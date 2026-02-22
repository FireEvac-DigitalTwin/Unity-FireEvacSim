using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public class AgentMover : MonoBehaviour
{
    public NavMeshAgent agent;

    [Header("Node Info")]
    public DecisionNode currentNode;

    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        // 시작 시 가장 가까운 노드 찾기
        DecisionNode nearest = FindNearestNode();

        if (nearest != null)
        {
            Debug.Log("🚀 시작 노드: " + nearest.nodeId);
            MoveToNode(nearest);
        }
        else
        {
            Debug.LogError("❌ 시작 노드를 찾지 못했습니다.");
        }
    }

    void Update()
    {
        if (agent.isStopped) return;

        // 목적지 도착 체크
        if (!agent.pathPending && agent.remainingDistance < 0.3f)
        {
            OnArriveAtNode(currentNode);
        }
    }

    void OnArriveAtNode(DecisionNode node)
    {
        if (node == null) return;

        Debug.Log("📍 도착 노드: " + node.nodeId);

        // 🔵 출구면 종료
        if (node.isExitNode)
        {
            Debug.Log("✅ 탈출 성공!");
            agent.isStopped = true;
            return;
        }

        DecisionNode next = ChooseNextNode(node);

        if (next == null)
        {
            Debug.Log("❌ 이동 가능한 노드가 없음 (불로 막힘)");
            agent.isStopped = true;
            return;
        }

        MoveToNode(next);
    }

    DecisionNode ChooseNextNode(DecisionNode node)
    {
        // 🔥 불이 아닌 노드만 선택
        var validNext = node.nextNodes
            .Where(n => !FireSpreadManager.Instance.IsNodeOnFire(n.transform.position))
            .ToList();

        if (validNext.Count == 0)
            return null;

        // ⭐ 유도 노드라면 방향 기반 선택
        if (node.isGuidanceNode)
        {
            DecisionNode guided =
                validNext
                .OrderByDescending(n =>
                    Vector3.Dot(
                        (n.transform.position - node.transform.position).normalized,
                        node.guidanceDirection))
                .First();

            Debug.Log("🟢 유도 방향 선택 → " + guided.nodeId);
            return guided;
        }

        // 🎲 일반 노드는 랜덤 선택
        DecisionNode randomNext =
            validNext[Random.Range(0, validNext.Count)];

        Debug.Log("🎲 랜덤 선택 → " + randomNext.nodeId);
        return randomNext;
    }

    void MoveToNode(DecisionNode node)
    {
        if (node == null) return;

        Debug.Log("➡ 이동: " +
            (currentNode != null ? currentNode.nodeId.ToString() : "Start")
            + " → " + node.nodeId);

        currentNode = node;
        agent.SetDestination(node.transform.position);
    }

    DecisionNode FindNearestNode()
    {
        DecisionNode[] nodes = FindObjectsOfType<DecisionNode>();

        if (nodes.Length == 0)
            return null;

        return nodes
            .OrderBy(n =>
                Vector3.Distance(transform.position, n.transform.position))
            .First();
    }
}
