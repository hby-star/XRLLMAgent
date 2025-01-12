using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class AgentFaceAnimationController : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer = null;

    #region Export BlendShape Info

    [Header("Export BlendShape Info")] private bool exportBlendShapeInfo = false;
    public string blendShapeInfoPath = "Assets/Scripts/Agent/BlendShapeInfo.json";

    [System.Serializable]
    public class BlendShapeInfo
    {
        public int index;
        public string name;
        public float weightMax = 100f;
        public float weightMin = 0f;
        public string description = "";
    }

    [System.Serializable]
    public class BlendShapeListWrapper
    {
        public List<BlendShapeInfo> blendShapes;
    }

    void ExportBlendShapeInfoStart()
    {
        if (!exportBlendShapeInfo) return;

        if (skinnedMeshRenderer == null)
        {
            Debug.LogError("SkinnedMeshRenderer is not assigned.");
            return;
        }

        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        List<BlendShapeInfo> blendShapeInfoList = new List<BlendShapeInfo>();

        // 获取所有的 BlendShape 名字和对应的权重
        int blendShapeCount = mesh.blendShapeCount;
        for (int i = 0; i < blendShapeCount; i++)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);
            float blendShapeWeight = skinnedMeshRenderer.GetBlendShapeWeight(i);

            BlendShapeInfo blendShapeInfo = new BlendShapeInfo
            {
                index = i,
                name = blendShapeName,
            };

            blendShapeInfoList.Add(blendShapeInfo);
        }

        // 转换为 JSON 字符串
        string json = JsonUtility.ToJson(new BlendShapeListWrapper { blendShapes = blendShapeInfoList }, true);

        // 保存到文件
        string path = blendShapeInfoPath;
        File.WriteAllText(path, json);

        Debug.Log("BlendShape info saved to: " + path);
    }

    #endregion


    #region Blink Controller

    [Header("Blink Controller")] [Tooltip("是否激活眨眼")]
    public bool enableBlink = true;

    [Tooltip("Blink BlendShape 索引")]public int blinkShapeIndex = 1;

    [Tooltip("眼睛闭上时的比例")] public float ratioClose = 100f;

    [Tooltip("眼睛睁开时的比例")] public float ratioOpen = 0f;

    [Tooltip("眼睛当前的比例")] public float currentRatio = 0f;

    [Tooltip("最小眨眼间隔")] public float minBlink = 2f;

    [Tooltip("最大眨眼间隔")] public float maxBlink = 4f;

    [Tooltip("最大眨眼时间")] public float timeBlink = 0.2f;

    [Tooltip("眨眼间隔计时器")] public float blinkTimer = 0f;

    [Tooltip("下次眨眼间隔")] private float blinkTime = 0;

    [Tooltip("眨眼的速度")] private float blinkSpeed;

    [Tooltip("是否在闭眼")] private bool closing = true;

    private bool blinking = true;

    void BlinkUpdate()
    {
        if (!enableBlink) return;

        if (blinking)
        {
            blinkSpeed = (ratioClose - ratioOpen) / timeBlink;
            if (closing)
            {
                currentRatio += Time.deltaTime * blinkSpeed;
            }
            else
            {
                currentRatio -= Time.deltaTime * blinkSpeed;
            }

            if (currentRatio < ratioOpen)
            {
                closing = true;

                currentRatio = ratioOpen;

                blinking = false;

                blinkTime = Random.Range(minBlink, maxBlink);

                blinkTimer = 0;
            }

            if (currentRatio > ratioClose)
            {
                closing = false;

                currentRatio = ratioClose;
            }

            skinnedMeshRenderer.SetBlendShapeWeight(blinkShapeIndex, currentRatio);
        }
        else
        {
            if (blinkTimer > blinkTime)
            {
                blinking = true;
            }
            else
            {
                blinkTimer += Time.deltaTime;
            }
        }
    }

    #endregion

    #region Handle Agent Function Call Event

    private void OnEnable()
    {
        Messenger<int, float>.AddListener(AgentFunctionCallEvent.FACE_ANIMATION_CALL, SingleFaceAnimationCall);
        Messenger.AddListener(AgentFunctionCallEvent.DEBUG_CALL, DebugCall);
    }

    private void OnDisable()
    {
        Messenger<int, float>.RemoveListener(AgentFunctionCallEvent.FACE_ANIMATION_CALL, SingleFaceAnimationCall);
        Messenger.RemoveListener(AgentFunctionCallEvent.DEBUG_CALL, DebugCall);
    }

    async void DebugCall()
    {
        Debug.Log("This is a debug call from AgentFunctionCallController");

        await Task.Run(() => Compute());

        Debug.Log("Debug call finished.");
    }

    void Compute()
    {
        // 模拟大约10秒的计算量
        double result = 0;
        DateTime startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalSeconds < 10)
        {
            for (int i = 0; i < 100000; i++)
            {
                result += Math.Sqrt(i);
            }
        }
    }


    public void SingleFaceAnimationCall(int index, float weight)
    {
        // Reset all other blend shapes
        for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
        {
            if (i != index)
            {
                skinnedMeshRenderer.SetBlendShapeWeight(i, 0);
            }
        }

        skinnedMeshRenderer.SetBlendShapeWeight(index, weight);
    }

    #endregion
    private void Start()
    {
        ExportBlendShapeInfoStart();
    }

    void Update()
    {
        BlinkUpdate();
    }
}