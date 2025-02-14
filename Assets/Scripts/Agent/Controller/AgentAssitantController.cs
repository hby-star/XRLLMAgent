using OpenAI.Assistants;
using OpenAI.Audio;
using OpenAI.Images;
using OpenAI.Models;
using OpenAI.Threads;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utilities.Async;
using Utilities.Audio;
using Utilities.Encoding.Wav;
using Utilities.Extensions;
using Utilities.WebRequestRest.Interfaces;
using ToolCall = OpenAI.Threads.ToolCall;

public class AgentAssitantController : MonoBehaviour
{
    [SerializeField] private bool enableDebug;

    [SerializeField] private AudioSource audioSource;

    [SerializeField] [Obsolete] private SpeechVoice voice;

    [SerializeField] [TextArea(3, 10)] private string systemPrompt =
        "You are a helpful assistant.\n- If an image is requested then use \"![Image](output.jpg)\" to display it.\n- When performing function calls, use the defaults unless explicitly told to use a specific value.\n- Images should always be generated in base64.";

    [SerializeField] private String newInput;

    private OpenAIClient openAI;
    private AssistantResponse assistant;
    private ThreadResponse thread;
    private readonly List<Tool> assistantTools = new();
    private readonly ConcurrentQueue<float> sampleQueue = new();

// #if !UNITY_2022_3_OR_NEWER
    private readonly CancellationTokenSource lifetimeCts = new();

    // ReSharper disable once InconsistentNaming
    private new CancellationToken destroyCancellationToken => lifetimeCts.Token;
// #endif

    private void OnValidate()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private async void Awake()
    {
        OnValidate();

        openAI = new OpenAIClient(
            new OpenAIAuthentication().LoadFromPath(Environment.CurrentDirectory + "/Assets/OpenAIKeys/APIKey.json"))
        {
            EnableDebug = enableDebug
        };

        // Add tools
        // for debug
        assistantTools.Add(
            Tool.GetOrCreateTool(typeof(AgentFunctionCallController), "DebugCall", "This is a debug call")
        );
        // for single face animation
        string singleFaceAnimationCallDescription = "Call this tool when you think you should make an expression." +
                                                    "The function takes two arguments: index and weight. " +
                                                    "Index is the index of the face animation. weight 0 means no animation, and weight 100 means full animation." +
                                                    "Here is the description for the arguments:" +
                                                    Resources.Load<TextAsset>("BlendShapeInfo").text;
        assistantTools.Add(
            Tool.GetOrCreateTool(typeof(AgentFunctionCallController), "SingleFaceAnimationCall",
                singleFaceAnimationCallDescription)
        );

        try
        {
            assistant = await openAI.AssistantsEndpoint.CreateAssistantAsync(
                new CreateAssistantRequest(
                    model: Model.GPT4o,
                    name: "OpenAI Sample Assistant",
                    description: "An assistant sample example for Unity",
                    instructions: systemPrompt,
                    tools: assistantTools),
                destroyCancellationToken);

            thread = await openAI.ThreadsEndpoint.CreateThreadAsync(
                new CreateThreadRequest(assistant),
                destroyCancellationToken);

            do
            {
                await Task.Yield();
            } while (!destroyCancellationToken.IsCancellationRequested);
        }
        catch (Exception e)
        {
            switch (e)
            {
                case ObjectDisposedException:
                    // ignored
                    break;
                default:
                    Debug.LogError(e);

                    break;
            }
        }
        finally
        {
            try
            {
                if (assistant != null)
                {
                    var deleteAssistantResult = await assistant.DeleteAsync(deleteToolResources: thread == null,
                        CancellationToken.None);

                    if (!deleteAssistantResult)
                    {
                        Debug.LogError("Failed to delete sample assistant!");
                    }
                }

                if (thread != null)
                {
                    var deleteThreadResult =
                        await thread.DeleteAsync(deleteToolResources: true, CancellationToken.None);

                    if (!deleteThreadResult)
                    {
                        Debug.LogError("Failed to delete sample thread!");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    private float lastPressTime;
    private float pressDelay = 1f;
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_canRecord && Time.time - lastPressTime > pressDelay)
            {
                lastPressTime = Time.time;
                ToggleRecording();
            }
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            if (Time.time - lastPressTime > pressDelay)
            {
                lastPressTime = Time.time;
                SubmitChat();
            }
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (sampleQueue.IsEmpty || !isGeneratingSpeech)
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

    private void OnDestroy()
    {
// #if !UNITY_2022_3_OR_NEWER
        lifetimeCts.Cancel();
        lifetimeCts.Dispose();
// #endif
    }


    private void SubmitChat(string _) => SubmitChat();

    private static bool isChatPending;

    private async void SubmitChat()
    {
        if (isChatPending || string.IsNullOrWhiteSpace(newInput))
        {
            return;
        }

        isChatPending = true;

        var userMessage = new Message(newInput);
        // var userMessageContent = AddNewTextMessageContent(Role.User);
        // userMessageContent.text = $"User: {newInput}";
        newInput = string.Empty;
        // var assistantMessageContent = AddNewTextMessageContent(Role.Assistant);
        // assistantMessageContent.text = "Assistant: ";

        try
        {
            await thread.CreateMessageAsync(userMessage, destroyCancellationToken);
            var run = await thread.CreateRunAsync(assistant, StreamEventHandler, destroyCancellationToken);
            await run.WaitForStatusChangeAsync(timeout: 60, cancellationToken: destroyCancellationToken);
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
            isChatPending = false;
        }

        async Task StreamEventHandler(IServerSentEvent @event)
        {
            try
            {
                switch (@event)
                {
                    case MessageResponse message:
                        switch (message.Status)
                        {
                            case MessageStatus.InProgress:
                                if (message.Role == Role.Assistant)
                                {
                                    // assistantMessageContent.text += message.PrintContent();
                                    // scrollView.verticalNormalizedPosition = 0f;
                                }

                                break;
                            case MessageStatus.Completed:
                                if (message.Role == Role.Assistant)
                                {
                                    await GenerateSpeechAsync(message.PrintContent(), destroyCancellationToken);
                                }

                                break;
                        }

                        break;
                    case RunResponse run:
                        switch (run.Status)
                        {
                            case RunStatus.RequiresAction:
                                await ProcessToolCalls(run);

                                break;
                        }

                        break;
                    case Error errorResponse:
                        throw errorResponse.Exception ?? new Exception(errorResponse.Message);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        async Task ProcessToolCalls(RunResponse run)
        {
            Debug.Log(nameof(ProcessToolCalls));
            var toolCalls = run.RequiredAction.SubmitToolOutputs.ToolCalls;

            var toolOutputs = await Task.WhenAll(toolCalls.Select(toolCall => ProcessToolCall(toolCall)))
                .ConfigureAwait(true);

            await run.SubmitToolOutputsAsync(new SubmitToolOutputsRequest(toolOutputs),
                cancellationToken: destroyCancellationToken);
        }

        async Task<ToolOutput> ProcessToolCall(ToolCall toolCall)
        {
            string result;

            try
            {
                await assistant.InvokeToolCallAsync<IReadOnlyList<ImageResult>>(toolCall,
                    destroyCancellationToken);
                result = "{\"result\":\"completed\"}";
            }
            catch (Exception e)
            {
                result = $"{{\"result\":\"{e.Message}\"}}";
            }

            return new ToolOutput(toolCall.Id, result);
        }
    }

    private static bool isGeneratingSpeech;

    private async Task GenerateSpeechAsync(string text, CancellationToken cancellationToken)
    {
        if (isGeneratingSpeech)
        {
            throw new InvalidOperationException("Speech generation is already in progress!");
        }

        if (enableDebug)
        {
            Debug.Log($"{nameof(GenerateSpeechAsync)}: {text}");
        }

        isGeneratingSpeech = true;

        try
        {
            sampleQueue.Clear();
            text = text.Replace("![Image](output.jpg)", string.Empty);

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
#pragma warning disable CS0612 // Type or member is obsolete
            var request = new SpeechRequest(text, Model.TTS_1, voice, SpeechResponseFormat.PCM);
#pragma warning restore CS0612 // Type or member is obsolete
            var lastProcessedSample = 0;

            var speechClip = await openAI.AudioEndpoint.GetSpeechAsync(request, partialCLip =>
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
        }
        finally
        {
            isGeneratingSpeech = false;
        }
    }

//         private TextMeshProUGUI AddNewTextMessageContent(Role role)
//         {
//             var textObject = new GameObject($"{contentArea.childCount + 1}_{role}");
//             textObject.transform.SetParent(contentArea, false);
//             var textMesh = textObject.AddComponent<TextMeshProUGUI>();
//             textMesh.fontSize = 24;
// #if UNITY_2023_1_OR_NEWER
//             textMesh.textWrappingMode = TextWrappingModes.Normal;
// #else
//             textMesh.enableWordWrapping = true;
// #endif
//             return textMesh;
//         }

    private bool _canRecord = true;

    private void ToggleRecording()
    {
        RecordingManager.EnableDebug = enableDebug;

        if (RecordingManager.IsRecording)
        {
            RecordingManager.EndRecording();

            if (enableDebug)
                Debug.Log("End recording");
        }
        else
        {
            if (enableDebug)
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
                await openAI.AudioEndpoint.CreateTranscriptionTextAsync(request, destroyCancellationToken);

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