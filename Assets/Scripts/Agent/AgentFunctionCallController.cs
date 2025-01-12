using System;
using System.Collections;
using System.Collections.Generic;
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
}