using System;
using System.Collections;
using System.Collections.Generic;
using OpenAI;
using OpenAI.Chat;
using UnityEngine;

public class AgentFunctionCallController : MonoBehaviour
{
    public static void DebugCall()
    {
        Messenger.Broadcast(AgentFunctionCallEvent.DEBUG_CALL);
    }

    public static void SingleFaceAnimationCall(long index, double weight)
    {
        Debug.Log("SingleFaceAnimationCall: (" + index + ", " + weight + ")");

        Messenger<int, float>.Broadcast(AgentFunctionCallEvent.FACE_ANIMATION_CALL, (int)index, (float)weight);
    }

    public static void SetLocalDestination(long x, long y, long z)
    {
        Messenger<Vector3>.Broadcast(AgentFunctionCallEvent.SET_LOCAL_DESTINATION, new Vector3(x, y, z));
    }

    public static string RagCall(string prompt)
    {
        return prompt;
    }
}