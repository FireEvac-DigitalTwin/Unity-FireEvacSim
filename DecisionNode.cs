using UnityEngine;
using System.Collections.Generic;

public class DecisionNode : MonoBehaviour
{
    public int nodeId;
    public List<DecisionNode> nextNodes = new List<DecisionNode>();

    [Header("Exit")]
    public bool isExitNode = false;

    [Header("Guidance (RL Result)")]
    public bool isGuidanceNode = false;
    public Vector3 guidanceDirection;

    public void SetGuidance(DecisionNode target)
    {
        isGuidanceNode = true;
        guidanceDirection =
            (target.transform.position - transform.position).normalized;
    }

    public void ClearGuidance()
    {
        isGuidanceNode = false;
    }

    public void SetGuidanceDirection(Vector3 newDir)
    {
        guidanceDirection = newDir.normalized;
    }

    public DecisionNode GetNextNode(Transform agent)
    {
        DecisionNode bestNode = null;
        float bestScore = -Mathf.Infinity;

        foreach (var next in nextNodes)
        {
            Vector3 dirToNext = (next.transform.position - transform.position).normalized;

            float score = Vector3.Dot(guidanceDirection, dirToNext);

            if (score > bestScore)
            {
                bestScore = score;
                bestNode = next;
            }
        }

        Debug.Log("Agent chose: " + bestNode.name);

        return bestNode;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, guidanceDirection * 2f);
    }

}
