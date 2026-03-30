using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AgentArrowFollower : MonoBehaviour
{
    [Header("시작 방식")]
    [Tooltip("true면 AgentMover 없이도 자동으로 화살표 추종을 시작합니다.")]
    public bool startAutomatically = true;

    [Tooltip("자동 시작일 때 대피 시작 딜레이(초)")]
    public float startDelay = 0f;

    [Tooltip("화살표에 이 거리까지 접근하면 다음 화살표로 이동 (m)")]
    public float arrivalThreshold = 0.9f;

    private NavMeshAgent      navAgent;
    private AgentMover        agentMover;
    private EvacuationAgent_NEW evac;
    private FloorArrow        currentTarget;
    private bool              following = false;
    private bool              headingToExitDoor = false;

    void Awake()
    {
        navAgent   = GetComponent<NavMeshAgent>();
        agentMover = GetComponent<AgentMover>();
        evac       = GetComponent<EvacuationAgent_NEW>();
    }

    void Start()
    {
        // DecisionNode/AgentMover를 안 쓰는 경우: 자동으로 화살표 추종 시작
        if (startAutomatically && agentMover == null)
            Invoke(nameof(BeginFollowing), Mathf.Max(0f, startDelay));
    }

    void Update()
    {
        if (evac != null && (evac.hasEvacuated || evac.isDead)) return;

        // 마지막 화살표(출구 방향 지시등) 이후에는 실제 출구(문)로 이동
        if (headingToExitDoor)
        {
            if (evac != null && evac.exitPoint != null)
            {
                // 목적지가 바뀌었거나 경로가 없을 때만 갱신
                if (!navAgent.pathPending && (navAgent.remainingDistance == Mathf.Infinity || navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f))
                    navAgent.SetDestination(evac.exitPoint.position);
            }
            return;
        }

        if (!following)
        {
            // AgentMover를 사용하는 경우(딜레이/이벤트 등): 기존대로 evacuationStarted를 기다림
            if (agentMover != null && agentMover.evacuationStarted)
                BeginFollowing();
            return;
        }

        if (navAgent.pathPending) return;

        if (navAgent.remainingDistance <= arrivalThreshold)
            AdvanceToNextArrow();
    }

    void BeginFollowing()
    {
        if (following) return;
        following     = true;
        currentTarget = FindNearestArrow();

        if (currentTarget != null)
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(currentTarget.transform.position);
            Debug.Log($"[{name}] 대피 시작 -> 첫 화살표: {currentTarget.name}");
        }
        else
        {
            Debug.LogWarning($"[{name}] 유효한 FloorArrow를 찾지 못했습니다.");
        }
    }

    void AdvanceToNextArrow()
    {
        if (currentTarget == null) return;

        // isExit == true인 FloorArrow는 "출구 방향을 가리키는 마지막 지시등"으로 취급
        if (currentTarget.isExit)
        {
            following = false;
            headingToExitDoor = true;

            if (evac != null && (evac.exitPoint != null || evac.EnsureExitPoint()))
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(evac.exitPoint.position);
            }
            else
            {
                Debug.LogWarning($"[{name}] exitPoint(문)를 찾지 못했습니다. 'Exit' 태그가 문 오브젝트에 설정되어 있는지 확인하세요.");
                navAgent.isStopped = true;
            }
            return;
        }

        var next = currentTarget.nextArrow;
        if (next == null)
        {
            currentTarget = FindNearestArrow();
            if (currentTarget != null)
                navAgent.SetDestination(currentTarget.transform.position);
            return;
        }

        currentTarget = next;
        navAgent.SetDestination(currentTarget.transform.position);
    }

    FloorArrow FindNearestArrow()
    {
        var all = FindObjectsByType<FloorArrow>(FindObjectsSortMode.None);
        FloorArrow best     = null;
        float      bestDist = float.MaxValue;

        foreach (var a in all)
        {
            if (a.isBlocked) continue;
            if (a.nextArrow == null && !a.isExit) continue;

            float d = Vector3.Distance(transform.position, a.transform.position);
            if (d < bestDist) { bestDist = d; best = a; }
        }

        return best;
    }
}
