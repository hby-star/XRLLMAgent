using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MxMGameplay
{
    public class AIDestinationSetter : MonoBehaviour
    {
        public List<Transform> destinations = new List<Transform>();
        public int currentDestinationIndex = 0;

        public Transform m_destinationTransform = null;
        [SerializeField] private float m_timeToChangeTarget = 5f;
        [SerializeField] private float m_patrolRadius = 30f;


        private Vector3 m_lastDestination = Vector3.zero;

        private NavMeshAgent m_navAgent;

        private float m_moveTimer = 0f;

        private void Start()
        {
            m_navAgent = GetComponent<NavMeshAgent>();

            if (m_navAgent && destinations.Count > 0)
            {
                m_destinationTransform = destinations[0];
                currentDestinationIndex = 0;
                m_navAgent.SetDestination(m_destinationTransform.position);
            }
        }

        private bool isChangingDestination = false;

        public void Update()
        {
            if (currentDestinationIndex < (destinations.Count - 1) && IsDestinationReached() && !isChangingDestination)
            {
                isChangingDestination = true;
                StartCoroutine(ChangeDestination());
            }
        }

        private IEnumerator ChangeDestination()
        {
            yield return new WaitForSeconds(m_timeToChangeTarget);

            currentDestinationIndex++;
            m_destinationTransform = destinations[currentDestinationIndex];
            m_navAgent.SetDestination(m_destinationTransform.position);
            isChangingDestination = false;
        }

        bool IsDestinationReached()
        {
            if (Vector3.Distance(m_navAgent.transform.position, destinations[currentDestinationIndex].position) < 1f)
            {
                return true;
            }

            return false;
        }
    }
}