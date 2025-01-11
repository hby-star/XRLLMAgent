using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using OpenAI;
using OpenAI.Models;
using OpenAI.Realtime;

public class TestRealTime : MonoBehaviour
{
    // Start is called before the first frame update
    async void Start()
    {
        await RealTimeSession();
    }

    private async Task RealTimeSession()
    {
        var api = new OpenAIClient(new OpenAIAuthentication().LoadFromPath(System.Environment.CurrentDirectory+"/Assets/OpenAIKeys/APIKey.json"));
        var cancellationTokenSource = new CancellationTokenSource();
        var tools = new List<Tool>
        {
            Tool.FromFunc("goodbye", () =>
            {
                cancellationTokenSource.Cancel();
                return "Goodbye!";
            })
        };
        var options = new Options(Model.GPT4oRealtime, tools: tools);
        using var session = await api.RealtimeEndpoint.CreateSessionAsync(options);
        var responseTask = session.ReceiveUpdatesAsync<IServerEvent>(ServerEvents, cancellationTokenSource.Token);
        await session.SendAsync(new ConversationItemCreateRequest("Hello!"));
        await session.SendAsync(new CreateResponseRequest());
        await session.SendAsync(new InputAudioBufferAppendRequest(new ReadOnlyMemory<byte>(new byte[1024 * 4])), cancellationTokenSource.Token);
        await session.SendAsync(new ConversationItemCreateRequest("GoodBye!"));
        await session.SendAsync(new CreateResponseRequest());
        await responseTask;
        Debug.Log("Realtime session completed.");
        Debug.Log(responseTask.ToString());


        void ServerEvents(IServerEvent @event)
        {
            switch (@event)
            {
                case ResponseAudioTranscriptResponse transcriptResponse:
                    Debug.Log(transcriptResponse.ToString());
                    break;
                case ResponseFunctionCallArgumentsResponse functionCallResponse:
                    if (functionCallResponse.IsDone)
                    {
                        ToolCall toolCall = functionCallResponse;
                        toolCall.InvokeFunction();
                    }

                    break;
            }
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }


}
