using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class EvacGuidanceAgent : Agent
{
    [Header("Environment")]
    public List<DecisionNode> allNodes;   // Node_0 ~ Node_n
    public int maxGuidanceCount = 3;      // 한 에피소드당 유도 노드 최대 개수

    [Header("Decision Settings")]
    public float decisionInterval = 0.2f; // 몇 초마다 decision 요청할지 (FixedUpdate 기준)

    private int placedCount = 0;
    private float decisionTimer = 0f;

    // =========================
    // Episode 시작
    // =========================
    public override void OnEpisodeBegin()
    {
        // 모든 노드 유도 초기화
        foreach (var node in allNodes)
        {
            if (node != null)
                node.ClearGuidance();
        }

        placedCount = 0;
        decisionTimer = 0f;

        // 필요 시 환경 리셋 연결
        // EvacuationManager.Instance.StartEpisode();
    }

    // =========================
    // 상태(State) 수집
    // =========================
    public override void CollectObservations(VectorSensor sensor)
    {
        // ⚠️ Behavior Parameters의 Vector Observation Size
        // = allNodes.Count 와 반드시 같아야 함
        foreach (var node in allNodes)
        {
            sensor.AddObservation(node != null && node.isGuidanceNode ? 1f : 0f);
        }
    }

    // =========================
    // 행동(Action) 처리
    // =========================
    public override void OnActionReceived(ActionBuffers actions)
    {
        // 이미 최대 개수만큼 설치했으면 더 안 함
        if (placedCount >= maxGuidanceCount)
            return;

        int nodeIndex = actions.DiscreteActions[0];
        int dirIndex  = actions.DiscreteActions[1];

        // 안전 체크
        if (nodeIndex < 0 || nodeIndex >= allNodes.Count)
        {
            AddReward(-0.2f);
            return;
        }

        DecisionNode node = allNodes[nodeIndex];
        if (node == null)
        {
            AddReward(-0.2f);
            return;
        }

        // 이미 유도 노드면 패널티
        if (node.isGuidanceNode)
        {
            AddReward(-0.1f);
            return;
        }

        // 연결된 다음 노드가 없으면 패널티
        if (node.nextNodes == null || node.nextNodes.Count == 0)
        {
            AddReward(-0.3f);
            return;
        }

        // 방향 선택 (범위 보호)
        DecisionNode target =
            node.nextNodes[dirIndex % node.nextNodes.Count];

        // 유도 설정
        node.SetGuidance(target);
        placedCount++;

        // 소량 패널티 (무분별 설치 방지)
        AddReward(-0.05f);
    }

    // =========================
    // ⭐ 강화학습 step 트리거 (핵심)
    // =========================
    void FixedUpdate()
    {
        // Python 학습 중일 때만 Decision 요청
        if (!Academy.Instance.IsCommunicatorOn)
            return;

        decisionTimer += Time.fixedDeltaTime;
        if (decisionTimer >= decisionInterval)
        {
            decisionTimer = 0f;
            RequestDecision();
        }
    }

    // =========================
    // 외부에서 호출: 에피소드 종료
    // =========================
    public void EndEpisodeWithResult(float avgEvacTime, int notEvacuatedCount)
    {
        float reward =
            -avgEvacTime * 0.01f
            -notEvacuatedCount * 1.0f;

        AddReward(reward);
        EndEpisode();
    }
}
