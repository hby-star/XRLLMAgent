using System;
using UnityEngine;
using UnityEngine.AI;

public class AgentMoveController : MonoBehaviour
{
    public NavMeshAgent navMeshAgent;

    private void Awake()
    {
        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgent is not assigned.");
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }

    #region Handle Agent Function Call Event

    private void OnEnable()
    {
        Messenger<Vector3>.AddListener(AgentFunctionCallEvent.SET_LOCAL_DESTINATION, SetLocalDestination);
    }

    private void OnDisable()
    {
        Messenger<Vector3>.RemoveListener(AgentFunctionCallEvent.SET_LOCAL_DESTINATION, SetLocalDestination);
    }

    private void SetLocalDestination(Vector3 destination)
    {
        Debug.Log("SetLocalDestination: " + destination * 5);
        Vector3 currentPos = transform.position;
        Vector3 destinationPos = currentPos + destination * 5;
        navMeshAgent.SetDestination(destinationPos);
    }

    #endregion
}