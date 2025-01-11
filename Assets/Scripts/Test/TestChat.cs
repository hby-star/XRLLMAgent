using System.Collections;
using System.Collections.Generic;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using UnityEngine;
using System.Threading.Tasks;  // Required for async/await support

public class TestChat : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
    }

    // The method that performs the asynchronous task
    private async void FetchChatResponseAsync()
    {
        try
        {
            var api = new OpenAIClient(new OpenAIAuthentication().LoadFromPath(System.Environment.CurrentDirectory + "/Assets/OpenAIKeys/APIKey.json"));
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, "Who won the world series in 2020?"),
                new Message(Role.Assistant, "The Los Angeles Dodgers won the World Series in 2020."),
                new Message(Role.User, "Where was it played?"),
            };

            var chatRequest = new ChatRequest(messages, Model.GPT4oMini);
            var response = await api.ChatEndpoint.GetCompletionAsync(chatRequest); // This is the async operation

            var choice = response.FirstChoice;
            Debug.Log($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");
        }
        catch (System.Exception ex)
        {
            // Handle any potential exceptions
            Debug.LogError($"Error fetching chat response: {ex.Message}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Call the async method
            FetchChatResponseAsync();
        }
    }
}