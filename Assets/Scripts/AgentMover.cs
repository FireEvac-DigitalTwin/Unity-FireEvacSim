using System.Collections; // ⭐ 딜레이를 위해 꼭 필요합니다
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AgentMover : MonoBehaviour
{
    private NavMeshAgent agent;

    private DecisionNode currentNode;
    private DecisionNode previousNode;   // 🔥 직전 노드 기억

    private EvacuationAgent_NEW evac;
    [SerializeField] private DecisionNode startNode;

    // RoomWanderer / AgentArrowFollower가 이 플래그를 보고 동작
    [HideInInspector] public bool evacuationStarted = false;

    // 대피 시작 시 한 번만 발동하는 이벤트 (EmergencyFlashEffect 등이 구독)
    public static event System.Action OnEvacuationStarted;
    private bool evacuationEventFired = false;

    // AgentArrowFollower가 붙어 있으면 DecisionNode 네비게이션은 건너뜀
    private bool hasArrowFollower;

    // ⭐ 추가됨: 유니티 인스펙터에서 수정 가능한 딜레이 변수
    [Header("대피 지연 설정")]
    public float evacuationDelay = 90f; // 기본값을 90초로 세팅

    [Header("병목 방지 설정 (Bottleneck Fix)")]
    [Tooltip("노드 도착 시 정확히 중심이 아닌 주변 몇 미터 안으로 갈지 설정")]
    public float targetJitterRadius = 1.2f;

    private float stuckTimer = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        evac  = GetComponent<EvacuationAgent_NEW>();
        hasArrowFollower = GetComponent<AgentArrowFollower>() != null;
    }

    void Start()
    {
        // FloorArrow(AgentArrowFollower) 기반이면 DecisionNode 시작 노드가 필요 없음
        // DecisionNode 기반 네비게이션을 사용할 때만 startNode가 필요합니다.

        // ⭐ 시작하자마자 냅다 뛰지 않고 딜레이 코루틴 실행
        // (hasArrowFollower == true 인 경우 DelayedInitialize가 evacuationStarted만 올리고 이동은 ArrowFollower가 처리)
        if (hasArrowFollower)
        {
            StartCoroutine(DelayedInitialize(evacuationDelay));
            return;
        }

        if (startNode != null)
        {
            StartCoroutine(DelayedInitialize(evacuationDelay));
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] AgentMover: startNode가 비어있습니다. DecisionNode를 사용하지 않으면 AgentMover를 제거하거나, FloorArrow라면 AgentArrowFollower를 사용하세요.");
            // startNode가 없으면 여기서 이동을 시작할 수 없음
        }
    }

    // ⭐ 추가됨: 지정된 시간만큼 멈춰있다가 출발시키는 마법의 함수
    IEnumerator DelayedInitialize(float delay)
    {
        // RoomWanderer가 있으면 배회를 맡김, 없으면 그냥 멈춤
        bool hasWanderer = GetComponent<RoomWanderer>() != null;
        if (agent != null && !hasWanderer)
            agent.isStopped = true;

        Debug.Log($"[{gameObject.name}] 화재 경보! {delay}초 동안 상황 파악 및 대기 중...");

        yield return new WaitForSeconds(delay);

        Debug.Log($"[{gameObject.name}] 대피 시작!");

        evacuationStarted = true;

        if (!evacuationEventFired)
        {
            evacuationEventFired = true;
            OnEvacuationStarted?.Invoke();
        }

        // AgentArrowFollower가 있으면 화살표 시스템이 이동을 처리함
        if (!hasArrowFollower)
            Initialize(startNode);
    }

    public void RestartEvacuation()
    {
        evacuationStarted = false;
        evacuationEventFired = false;
        StartCoroutine(DelayedInitialize(evacuationDelay));
    }

    public void Initialize(DecisionNode startNode)
    {
        currentNode = startNode;
        previousNode = null;

        if (agent == null) return;

        agent.isStopped = false;
        SetDestinationWithJitter(currentNode.transform.position);
    }

    // ⭐ 병목 방지: 목적지를 노드 중심이 아닌 주변 랜덤 위치로 분산
    void SetDestinationWithJitter(Vector3 targetPos)
    {
        Vector3 jitter = Random.insideUnitSphere * targetJitterRadius;
        jitter.y = 0;

        Vector3 jitteredTarget = targetPos + jitter;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(jitteredTarget, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.SetDestination(targetPos);
        }
    }

    void Update()
    {
        if (evac == null || evac.hasEvacuated || evac.isDead)
            return;

        // 화살표 시스템이 네비게이션을 담당하면 여기서 종료
        if (hasArrowFollower) return;
        if (agent == null || !evacuationStarted) return;

        // 🔥 병목 방지: 에이전트가 1.5초 이상 멈춰있으면 회피 우선순위를 높여서 탈출
        if (agent.velocity.sqrMagnitude < 0.01f && agent.remainingDistance > agent.stoppingDistance)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.5f)
            {
                agent.avoidancePriority = Random.Range(0, 30);
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        if (agent.pathPending) return;

        if (agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            OnArrivedAtNode();
        }
    }

    void OnArrivedAtNode()
    {
        if (currentNode == null) return;

        // 🔥 출구 도착
        if (currentNode.isExitNode)
        {
            evac.Evacuate();
            agent.isStopped = true;
            return;
        }

        DecisionNode nextNode = ChooseNextNode();

        if (nextNode == null) return;

        previousNode = currentNode;
        currentNode = nextNode;

        SetDestinationWithJitter(currentNode.transform.position);
    }

    DecisionNode ChooseNextNode()
    {
        if (currentNode.nextNodes == null || currentNode.nextNodes.Count == 0)
            return null;

        List<DecisionNode> candidates = new List<DecisionNode>();

        foreach (var node in currentNode.nextNodes)
        {
            if (node != previousNode)
                candidates.Add(node);
        }

        // 🔥 막다른 길이면 어쩔 수 없이 뒤로 감
        if (candidates.Count == 0)
            candidates = currentNode.nextNodes;

        // 🔥 Guidance 노드일 경우 방향 기반 선택
        if (currentNode.isGuidanceNode)
        {
            Vector3 guideDir = currentNode.guidanceDirection.normalized;

            DecisionNode bestNode = null;
            float bestDot = -999f;

            foreach (var node in candidates)
            {
                Vector3 dir = (node.transform.position - currentNode.transform.position).normalized;
                float dot = Vector3.Dot(guideDir, dir);

                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestNode = node;
                }
            }

            return bestNode;
        }

        // 🔥 일반 노드 → 랜덤 선택
        return candidates[Random.Range(0, candidates.Count)];
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Exit"))
        {
            if (GuidanceManager.Instance != null)
                GuidanceManager.Instance.RewardForEvacuation();
            gameObject.SetActive(false);
        }
        else if (other.CompareTag("Fire"))
        {
            if (GuidanceManager.Instance != null)
                GuidanceManager.Instance.PenaltyForDeath();
            gameObject.SetActive(false);
        }
    }

    // 외부에서 리셋용
    public void ResetDestination()
    {
        StopAllCoroutines();

        evacuationStarted = false;
        evacuationEventFired = false;
        currentNode = startNode;
        previousNode = null;

        if (agent != null)
        {
            agent.isStopped = true;
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.ResetPath();
        }

        if (hasArrowFollower || startNode != null)
            StartCoroutine(DelayedInitialize(evacuationDelay));
    }
}