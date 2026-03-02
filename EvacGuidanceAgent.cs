using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class EvacGuidanceAgent : Agent
{
    [Header("Decision Settings")]
    public float decisionInterval = 0.2f; // 몇 초마다 decision 요청할지 (FixedUpdate 기준)

    private float decisionTimer = 0f;

    // =========================
    // Episode 시작
    // =========================
    public override void OnEpisodeBegin()
    {
        decisionTimer = 0f;
    }

    // =========================
    // 상태(State) 수집 (노드 제거 후 최소 관측 - 강화학습 환경 호환)
    // =========================
    public override void CollectObservations(VectorSensor sensor)
    {
        // Vector Observation Size = 1 로 설정 필요 (Behavior Parameters)
        sensor.AddObservation(0f);
    }

    // =========================
    // 행동(Action) 처리 (노드 제거 후 동작 없음 - 추후 확장 가능)
    // =========================
    public override void OnActionReceived(ActionBuffers actions)
    {
        // 노드 기반 유도 제거됨. 필요 시 여기에 새 행동 공간 추가
    }

    // =========================
    // 강화학습 step 트리거
    // =========================
    void FixedUpdate()
    {
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
            - notEvacuatedCount * 1.0f;

        AddReward(reward);
        EndEpisode();
    }
}