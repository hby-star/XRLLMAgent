using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Images;
using OpenAI.Models;
using OpenAI;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utilities.Async;
using Utilities.Audio;
using Utilities.Encoding.Wav;
using Utilities.Extensions;
using Utilities.WebRequestRest;
using Microphone = UnityEngine.Microphone;

public class TestAudio : MonoBehaviour
{
    [SerializeField] private bool enableDebug;

    [SerializeField] private AudioSource audioSource;

    [Obsolete] [SerializeField] private SpeechVoice voice;

    [SerializeField] [TextArea(3, 10)] private string systemPrompt =
        "You are a helpful assistant.\n- If an image is requested then use \"![Image](output.jpg)\" to display it.\n- When performing function calls, use the defaults unless explicitly told to use a specific value.\n- Images should always be generated in base64.";

    [SerializeField] private String newInput;

    private OpenAIClient _openAIClient;
    private readonly Conversation _conversation = new();
    private readonly List<Tool> assistantTools = new();
    private readonly ConcurrentQueue<float> sampleQueue = new();

#if !UNITY_2022_3_OR_NEWER
        private readonly CancellationTokenSource lifetimeCts = new();
        // ReSharper disable once InconsistentNaming
        private CancellationToken destroyCancellationToken => lifetimeCts.Token;
#endif

    private void OnValidate()
    {
        if (audioSource == null)
        {
            audioSource = GetComponentInChildren<AudioSource>();
        }
    }

    private void Awake()
    {
        OnValidate();
        _openAIClient = new OpenAIClient(
            new OpenAIAuthentication().LoadFromPath(Environment.CurrentDirectory + "/Assets/OpenAIKeys/APIKey.json"));
        ConversionAppendMessage(Role.System, systemPrompt);
    }

    private float lastPressTime;
    private float pressDelay = 1f;
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            if(_canRecord && Time.time - lastPressTime > pressDelay)
            {
                lastPressTime = Time.time;
                ToggleRecording();
            }
        }
    }

    private void ConversionAppendMessage(Role role, string content)
    {
        _conversation.AppendMessage(new Message(role, content));
        if (enableDebug)
        {
            if (role == Role.System)
                Debug.Log("System: " + content);
            else if (role == Role.User)
                Debug.Log("User: " + content);
            else if (role == Role.Assistant)
                Debug.Log("Assistant: " + content);
        }
    }


#if !UNITY_2022_3_OR_NEWER
        private void OnDestroy()
        {
            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
        }
#endif

    private static bool _isChatPending;

    private async void SubmitChat()
    {
        if (_isChatPending || string.IsNullOrWhiteSpace(newInput))
        {
            return;
        }

        if (enableDebug)
            Debug.Log("Enter SubmitChat");

        _isChatPending = true;

        ConversionAppendMessage(Role.User, newInput);

        //var userMessageContent = AddNewTextMessageContent(Role.User);
        //userMessageContent.text = $"User: {newInput}";
        newInput = string.Empty;
        //var assistantMessageContent = AddNewTextMessageContent(Role.Assistant);
        //assistantMessageContent.text = "Assistant: ";

        try
        {
            //var request = new ChatRequest(_conversation.Messages, tools: assistantTools);
            var request = new ChatRequest(_conversation.Messages);
            var response = await _openAIClient.ChatEndpoint.StreamCompletionAsync(request,
                resultHandler: deltaResponse =>
                {
                    if (deltaResponse?.FirstChoice?.Delta == null)
                    {
                        return;
                    }

                    //assistantMessageContent.text += deltaResponse.FirstChoice.Delta.ToString();
                }, cancellationToken: destroyCancellationToken);

            ConversionAppendMessage(response.FirstChoice.Message.Role, response.FirstChoice.Message.Content.ToString());

            if (response.FirstChoice.FinishReason == "tool_calls")
            {
                response = await ProcessToolCallsAsync(response);
            }

            await GenerateSpeechAsync(response, destroyCancellationToken);
        }
        catch (Exception e)
        {
            switch (e)
            {
                case TaskCanceledException:
                case OperationCanceledException:
                    break;
                default:
                    Debug.LogError(e);
                    break;
            }
        }
        finally
        {
            _isChatPending = false;
        }

        async Task<ChatResponse> ProcessToolCallsAsync(ChatResponse response)
        {
            var toolCalls = new List<Task>();

            foreach (var toolCall in response.FirstChoice.Message.ToolCalls)
            {
                if (enableDebug)
                {
                    Debug.Log(
                        $"{response.FirstChoice.Message.Role}: {toolCall.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
                    Debug.Log($"{toolCall.Function.Arguments}");
                }

                toolCalls.Add(ProcessToolCall());

                async Task ProcessToolCall()
                {
                    await Awaiters.UnityMainThread;

                    try
                    {
                        var results = await toolCall.InvokeFunctionAsync<string>(destroyCancellationToken)
                            .ConfigureAwait(true);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                        _conversation.AppendMessage(new(toolCall, $"{{\"result\":\"{e.Message}\"}}"));
                        return;
                    }

                    _conversation.AppendMessage(new(toolCall, "{\"result\":\"completed\"}"));
                }
            }


            await Task.WhenAll(toolCalls).ConfigureAwait(true);
            ChatResponse toolCallResponse;

            try
            {
                var toolCallRequest = new ChatRequest(_conversation.Messages, tools: assistantTools);
                toolCallResponse =
                    await _openAIClient.ChatEndpoint.GetCompletionAsync(toolCallRequest, destroyCancellationToken);
                _conversation.AppendMessage(toolCallResponse.FirstChoice.Message);
            }
            catch (RestException restEx)
            {
                Debug.LogError(restEx);

                foreach (var toolCall in response.FirstChoice.Message.ToolCalls)
                {
                    _conversation.AppendMessage(new Message(toolCall, restEx.Response.Body));
                }

                var toolCallRequest = new ChatRequest(_conversation.Messages, tools: assistantTools);
                toolCallResponse =
                    await _openAIClient.ChatEndpoint.GetCompletionAsync(toolCallRequest, destroyCancellationToken);
                _conversation.AppendMessage(toolCallResponse.FirstChoice.Message);
            }

            if (toolCallResponse.FirstChoice.FinishReason == "tool_calls")
            {
                return await ProcessToolCallsAsync(toolCallResponse);
            }

            return toolCallResponse;
        }
    }

    private volatile bool isSpeechGenerationCompleted = false;

    private async Task GenerateSpeechAsync(string text, CancellationToken cancellationToken)
    {
        text = text.Replace("![Image](output.jpg)", string.Empty);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            sampleQueue.Clear();
            isSpeechGenerationCompleted = false;

#pragma warning disable CS0612 // Type or member is obsolete
            var request = new SpeechRequest(text, Model.TTS_1, voice, SpeechResponseFormat.PCM);
#pragma warning restore CS0612 // Type or member is obsolete

            var lastProcessedSample = 0;

            var speechClip = await _openAIClient.AudioEndpoint.GetSpeechAsync(request, partialCLip =>
            {
                if (partialCLip.AudioSamples.Length > lastProcessedSample)
                {
                    lastProcessedSample = partialCLip.AudioSamples.Length;

                    var newSamples = partialCLip.AudioSamples[lastProcessedSample..];

                    foreach (var sample in newSamples)
                    {
                        sampleQueue.Enqueue(sample);
                    }
                }
            }, cancellationToken);

            audioSource.clip = speechClip.AudioClip;
            audioSource.Play();

            while (audioSource.isPlaying && !cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }

            isSpeechGenerationCompleted = true;
            Debug.Log("Speech generation completed.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"GenerateSpeechAsync failed: {ex.Message}");
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (sampleQueue.IsEmpty || isSpeechGenerationCompleted)
        {
            return;
        }

        for (var i = 0; i < data.Length; i += channels)
        {
            if (sampleQueue.TryDequeue(out var sample))
            {
                for (var j = 0; j < channels; j++)
                {
                    data[i + j] = sample;
                }
            }
            else
            {
                for (var j = 0; j < channels; j++)
                {
                    data[i + j] = 0f; // Fill silence if queue is empty
                }
            }
        }
    }

    private TextMeshProUGUI AddNewTextMessageContent(Role role)
    {
        var textObject = new GameObject($"{role}");
        var textMesh = textObject.AddComponent<TextMeshProUGUI>();
        textMesh.fontSize = 24;
#if UNITY_2023_1_OR_NEWER
            textMesh.textWrappingMode = TextWrappingModes.Normal;
#else
        textMesh.enableWordWrapping = true;
#endif
        return textMesh;
    }

    private bool _canRecord = true;

    private void ToggleRecording()
    {
        RecordingManager.EnableDebug = enableDebug;

        if (RecordingManager.IsRecording)
        {
            RecordingManager.EndRecording();

            if(enableDebug)
                Debug.Log("End recording");
        }
        else
        {
            if(enableDebug)
                Debug.Log("Start recording");

            // ReSharper disable once MethodSupportsCancellation
            RecordingManager.StartRecording<WavEncoder>(callback: ProcessRecording);
        }
    }

    private async void ProcessRecording(Tuple<string, AudioClip> recording)
    {
        var (path, clip) = recording;

        if (enableDebug)
        {
            Debug.Log(path);
        }

        try
        {
            //recordButton.interactable = false;
            var request = new AudioTranscriptionRequest(clip, temperature: 0.1f, language: "en");
            var userInput =
                await _openAIClient.AudioEndpoint.CreateTranscriptionTextAsync(request, destroyCancellationToken);

            if (enableDebug)
            {
                Debug.Log(userInput);
            }

            newInput = userInput;
            SubmitChat();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            _canRecord = true;
        }
    }
}