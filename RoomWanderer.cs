using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 대피 경보 전 대기 시간 동안 방 안에서 자연스럽게 배회합니다.
/// AgentMover.evacuationStarted == true 가 되면 제어를 AgentMover에 넘깁니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class RoomWanderer : MonoBehaviour
{
    [Header("배회 설정")]
    [Tooltip("시작 위치로부터 배회할 최대 반경(m)")]
    public float wanderRadius = 4f;
    public float minWaitTime  = 2f;
    public float maxWaitTime  = 6f;

    private NavMeshAgent navAgent;
    private AgentMover   agentMover;
    private Vector3      homePosition;
    private float        waitTimer;
    private bool         waiting = true;

    void Start()
    {
        navAgent   = GetComponent<NavMeshAgent>();
        agentMover = GetComponent<AgentMover>();
        homePosition = transform.position;

        // 에이전트마다 시작 타이밍을 분산
        waitTimer = Random.Range(0f, maxWaitTime);
    }

    void Update()
    {
        // 대피가 시작되면 이 컴포넌트는 비활성화
        if (agentMover != null && agentMover.evacuationStarted)
        {
            // NavMeshAgent 제어를 AgentMover에 돌려줌
            navAgent.isStopped = false;
            enabled = false;
            return;
        }

        if (navAgent == null || !navAgent.isOnNavMesh) return;

        // 목적지 도착 감지
        if (!waiting && !navAgent.pathPending &&
            navAgent.remainingDistance <= navAgent.stoppingDistance + 0.2f)
        {
            waiting   = true;
            waitTimer = Random.Range(minWaitTime, maxWaitTime);
        }

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                MoveToRandomPoint();
            }
        }
    }

    void MoveToRandomPoint()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector3 offset    = Random.insideUnitSphere * wanderRadius;
            offset.y          = 0f;
            Vector3 candidate = homePosition + offset;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(hit.position);
                return;
            }
        }

        // 유효한 위치를 못 찾으면 잠시 더 대기
        waiting   = true;
        waitTimer = maxWaitTime;
    }
}
