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
}
