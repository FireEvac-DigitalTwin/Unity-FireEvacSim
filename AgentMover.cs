using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AgentMover : MonoBehaviour
{
    private NavMeshAgent agent;
    private DecisionNode currentNode;
    private DecisionNode previousNode;

    private EvacuationAgent_NEW evac;
    [SerializeField] private DecisionNode startNode;

    [HideInInspector] public bool evacuationStarted = false;
    public static event System.Action OnEvacuationStarted;
    private static bool evacuationEventFired = false;

    private bool hasArrowFollower;

    [Header("대피 지연 설정")]
    public float evacuationDelay = 1.0f; // 테스트를 위해 기본값 낮춤

    [Header("병목 방지 설정 (Bottleneck Fix)")]
    [Tooltip("노드 도착 시 정확히 중심이 아닌 주변 몇 미터 안으로 갈지 설정")]
    public float targetJitterRadius = 1.2f; 
    
    private Vector3 currentRandomizedTarget;
    private float stuckTimer = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        evac  = GetComponent<EvacuationAgent_NEW>();
        hasArrowFollower = GetComponent<AgentArrowFollower>() != null;
    }

    void Start()
    {
        // 1. 에이전트 간의 겹침을 허용하고 싶다면 Radius를 살짝 줄이는 것이 좋습니다.
        if (agent != null) {
            // 인스펙터에서도 조절 가능하지만 코드로 강제 최적화
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        if (hasArrowFollower || startNode != null)
        {
            StartCoroutine(DelayedInitialize(evacuationDelay));
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] AgentMover: startNode가 비어있습니다.");
        }
    }

    IEnumerator DelayedInitialize(float delay)
    {
        bool hasWanderer = GetComponent<RoomWanderer>() != null;
        if (agent != null && !hasWanderer)
            agent.isStopped = true;

        yield return new WaitForSeconds(delay);

        evacuationStarted = true;

        if (!evacuationEventFired)
        {
            evacuationEventFired = true;
            OnEvacuationStarted?.Invoke();
        }

        if (!hasArrowFollower)
            Initialize(startNode);
    }

    public void Initialize(DecisionNode startNode)
    {
        currentNode = startNode;
        previousNode = null;

        if (agent == null || currentNode == null) return;

        agent.isStopped = false;
        SetDestinationWithJitter(currentNode.transform.position);
    }

    void Update()
    {
        if (evac == null || evac.hasEvacuated || evac.isDead) return;
        if (hasArrowFollower || agent == null || !evacuationStarted) return;

        // 🔥 병목 방지 로직: 에이전트가 속도가 거의 없는데 목표는 멀다면 'Stuck' 상태로 간주
        if (agent.velocity.sqrMagnitude < 0.01f && agent.remainingDistance > agent.stoppingDistance)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.5f) // 1.5초 이상 갇혀있다면
            {
                // 우선순위를 일시적으로 높여(낮은 숫자) 길을 비집고 나가게 함
                agent.avoidancePriority = Random.Range(0, 30);
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        if (agent.pathPending) return;

        // 도착 판단
        if (agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            OnArrivedAtNode();
        }
    }

    // ⭐ 핵심: 목적지 분산 로직
    void SetDestinationWithJitter(Vector3 targetPos)
    {
        // 노드의 중심에서 targetJitterRadius 만큼 랜덤한 위치를 계산 (바닥면 X, Z만)
        Vector3 jitter = Random.insideUnitSphere * targetJitterRadius;
        jitter.y = 0; 
        
        currentRandomizedTarget = targetPos + jitter;
        
        // 유효한 NavMesh 위인지 확인 후 설정
        NavMeshHit hit;
        if (NavMesh.SamplePosition(currentRandomizedTarget, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.SetDestination(targetPos);
        }
    }

    void OnArrivedAtNode()
    {
        if (currentNode == null) return;

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

        // 다음 노드로 갈 때도 랜덤 좌표 부여
        SetDestinationWithJitter(currentNode.transform.position);
    }

    DecisionNode ChooseNextNode()
    {
        if (currentNode.nextNodes == null || currentNode.nextNodes.Count == 0)
            return null;

        List<DecisionNode> candidates = new List<DecisionNode>();
        foreach (var node in currentNode.nextNodes)
        {
            if (node != previousNode) candidates.Add(node);
        }

        if (candidates.Count == 0) candidates = currentNode.nextNodes;

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

        return candidates[Random.Range(0, candidates.Count)];
    }

    public void ResetDestination()
    {
        if (agent == null || currentNode == null) return;
        agent.ResetPath();
        agent.isStopped = false;
        SetDestinationWithJitter(currentNode.transform.position);
    }

    // 트리거 로직 유지
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Exit")) { gameObject.SetActive(false); }
        else if (other.CompareTag("Fire")) { gameObject.SetActive(false); }
    }
}