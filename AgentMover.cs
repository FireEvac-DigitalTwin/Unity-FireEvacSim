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

    // ⭐ 추가됨: 유니티 인스펙터에서 수정 가능한 딜레이 변수
    [Header("대피 지연 설정")]
    public float evacuationDelay = 90f; // 기본값을 90초로 세팅

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        evac = GetComponent<EvacuationAgent_NEW>();
    }

    void Start()
    {
        if (startNode != null)
        {
            // ⭐ 시작하자마자 냅다 뛰지 않고 딜레이 코루틴 실행
            StartCoroutine(DelayedInitialize(evacuationDelay));
        }
        else
        {
            Debug.LogError("Start Node가 설정되지 않았습니다!");
        }
    }

    // ⭐ 추가됨: 지정된 시간만큼 멈춰있다가 출발시키는 마법의 함수
    IEnumerator DelayedInitialize(float delay)
    {
        if (agent != null) 
            agent.isStopped = true; // 일단 발을 묶음

        Debug.Log($"[{gameObject.name}] 화재 경보! {delay}초 동안 상황 파악 및 대기 중...");

        // 설정한 시간(90초)만큼 여기서 코드가 멈춰서 기다립니다.
        yield return new WaitForSeconds(delay);

        Debug.Log($"[{gameObject.name}] 대피 시작!");

        // 대기 시간이 끝나면 원래 하던 대로 목적지를 향해 출발!
        Initialize(startNode);
    }

    public void Initialize(DecisionNode startNode)
    {
        currentNode = startNode;
        previousNode = null;

        if (agent == null) return;

        agent.isStopped = false;
        agent.SetDestination(currentNode.transform.position);
    }

    void Update()
    {
        if (evac == null || evac.hasEvacuated || evac.isDead)
            return;

        if (agent.pathPending) return;

        if (agent.remainingDistance <= agent.stoppingDistance)
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

        agent.SetDestination(currentNode.transform.position);
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

    // 외부에서 리셋용
    public void ResetDestination()
    {
        if (agent == null || currentNode == null) return;

        agent.ResetPath();
        agent.isStopped = false;
        agent.SetDestination(currentNode.transform.position);
    }
}