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
        // 필요하다면 여기에 에이전트를 시작 위치로 되돌리는 로직을 넣을 수 있습니다.
    }

    // =========================
    // 상태(State) 수집
    // =========================
    public override void CollectObservations(VectorSensor sensor)
    {
        // 예시: 0만 넣지 말고, 주변 상황을 관측할 수 있는 데이터를 최소한 넣어주어야 합니다.
        // 유도등의 현재 방향, 주요 거점의 혼잡도, 화재 센서 데이터 등 (총 n개)
        sensor.AddObservation(this.transform.position); // 에이전트 위치 (Vector3 = 3개)
        // 유니티 인스펙터 창의 Behavior Parameters -> Vector Observation -> Space Size도 3으로 맞춰야 합니다.
    }

    // =========================
    // 행동(Action) 처리
    // =========================
    public override void OnActionReceived(ActionBuffers actions)
    {
        // 1. AI가 선택한 행동 번호 가져오기 (0, 1, 2, 3...)
        int action = actions.DiscreteActions[0];

        // 2. 행동에 따른 실제 로직 처리
        // 가영 님의 유도등(Guidance Lights) 오브젝트나 매니저를 여기서 조절합니다.
        switch (action)
        {
            case 0: // 북쪽(North)
                UpdateGuidanceDirection(Vector3.forward); 
                break;
            case 1: // 남쪽(South)
                UpdateGuidanceDirection(Vector3.back);
                break;
            case 2: // 동쪽(East)
                UpdateGuidanceDirection(Vector3.right);
                break;
            case 3: // 서쪽(West)
                UpdateGuidanceDirection(Vector3.left);
                break;
            default: // 정지 또는 대기
                // 아무것도 안 함
                break;
        }

        // 3. 보상 설정 (중요!)
        // 예: 탈출 인원이 늘어났다면 보상을 줍니다.
        float currentEvacuated = EvacuationManager.Instance.GetEvacuatedCount();
        if (currentEvacuated > lastEvacuatedCount)
        {
            AddReward(1.0f); // 잘했어!
            lastEvacuatedCount = currentEvacuated;
        }

        // 4. 에피소드 종료 조건
        if (EvacuationManager.Instance.IsAllEvacuated())
        {
            EndEpisode();
        }
    }

    // 유도등의 화살표 방향이나 물리적 힘을 바꿔주는 함수 (가영 님 기존 코드에 맞춰 수정 필요)
    void UpdateGuidanceDirection(Vector3 direction)
    {
        // 예: 모든 에이전트의 목표 지점을 이 방향으로 업데이트하거나,
        // 유도등 프리팹의 화살표 텍스처를 바꿉니다.
        EvacuationManager.Instance.SetGlobalGuidance(direction);
    }
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

    // ==========================================
    // 🔥 사진 속 개별 보상 함수 적용 (트리거 충돌)
    // ==========================================
    private void OnTriggerEnter(Collider other)
    {
        // 1. 목적지(비상구) 도달 : +1.0
        if (other.CompareTag("Exit"))
        {
            AddReward(1.0f);
            EndEpisode(); // 도달했으므로 에피소드 종료
        }
        // 2. 불 도달 : -1.0
        else if (other.CompareTag("Fire"))
        {
            AddReward(-1.0f);
            EndEpisode(); // 실패했으므로 에피소드 종료
        }
        // 3. 벽 도달 : -0.01
        else if (other.CompareTag("Wall"))
        {
            AddReward(-0.01f);
            // 사진 설명처럼 벽에 닿았을 때도 에피소드를 끝내려면 아래 주석을 해제하세요.
            //EndEpisode(); 
        }
    }


    public void EndEpisodeWithResult(float avgEvacTime, int notEvacuatedCount)
    {
        // 훈련 스케일에 맞추어 마이너스 강도를 낮추어 줍니다.
        float reward = -avgEvacTime * 0.001f - notEvacuatedCount * 0.1f;

        AddReward(reward);
        EndEpisode();
    }
}