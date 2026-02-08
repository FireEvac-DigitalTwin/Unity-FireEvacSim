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

        // 시작 시 가장 가까운 노드로 이동
        DecisionNode nearest = FindNearestNode();
        MoveToNode(nearest);
    }

    void Update()
    {
        // 목적지 도착 체크
        if (!agent.pathPending && agent.remainingDistance < 0.3f)
        {
            OnArriveAtNode(currentNode);
        }
    }

    void OnArriveAtNode(DecisionNode node)
    {
        if (node == null) return;

        // 출구면 멈춤
        if (node.isExitNode)
        {
            agent.isStopped = true;
            return;
        }

        DecisionNode next = ChooseNextNode(node);
        MoveToNode(next);
    }

    DecisionNode ChooseNextNode(DecisionNode node)
    {
        var validNext = node.nextNodes.FindAll(n => n != null);
        if (validNext.Count == 0) return null;

        // ⭐ 유도 노드면 그 방향을 가장 잘 따르는 노드 선택
        if (node.isGuidanceNode)
        {
            return validNext
                .OrderByDescending(n =>
                    Vector3.Dot(
                        (n.transform.position - node.transform.position).normalized,
                        node.guidanceDirection))
                .First();
        }

        // 아니면 랜덤
        return validNext[Random.Range(0, validNext.Count)];
    }



    void MoveToNode(DecisionNode node)
    {
        if (node == null) return;
        currentNode = node;
        agent.SetDestination(node.transform.position);
    }

    DecisionNode FindNearestNode()
    {
        DecisionNode[] nodes = FindObjectsOfType<DecisionNode>();
        return nodes
            .OrderBy(n => Vector3.Distance(transform.position, n.transform.position))
            .First();
    }
}
