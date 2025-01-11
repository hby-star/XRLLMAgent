using System.Collections;
using System.Collections.Generic;
using OpenAI;
using UnityEngine;

public class TestAPIKey : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var api = new OpenAIClient(new OpenAIAuthentication().LoadFromPath(System.Environment.CurrentDirectory+"/Assets/OpenAIKeys/APIKey.json"));
        // 打印API Key
        Debug.Log(api.HasValidAuthentication);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
