using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파이썬 서버의 에이전트 데이터를 Unity 씬의 에이전트 GameObjects와 동기화합니다.
/// Procedural Human Animator를 제어하여 병목 현상을 시각화합니다.
/// </summary>
public class AgentSynchronizer : MonoBehaviour
{
    // 💡 에이전트의 컴포넌트들을 미리 저장해두는 캐싱 클래스 (최적화용)
    private class AgentComponents
    {
        public GameObject go;
        public ProceduralHumanAnimator procAnimator; // 기본 Animator 대신 스크립트 연결
        public Renderer renderer;
        public ParticleSystem sweatParticle;
    }

    [Header("이동 설정")]
    [SerializeField] private float cellSize = 5f;
    [SerializeField] private float agentHeight = 0.5f; // 구체의 중심 높이에 맞게 조정
    [SerializeField] private bool useSmoothing = true;
    [SerializeField] private float baseWalkSpeed = 15f; 
    [SerializeField] private float rotationSpeed = 10f;

    [Header("군중 물리 시각화 (Speed & Panic)")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color panicColor = new Color(1f, 0.3f, 0.3f); // 살짝 붉은색
    [SerializeField] private float panicThresholdForSweat = 0.6f; // 패닉이 0.6 이상일 때 땀 파티클 켜짐

    [Header("참조")]
    [SerializeField] private PythonBridge pythonBridge;
    [SerializeField] private Transform gridOrigin;  // 그리드의 (0,0) 위치
    [SerializeField] private GameObject agentPrefab; // ProceduralHumanAnimator가 달린 프리팹

    private Dictionary<int, AgentComponents> activeAgents = new Dictionary<int, AgentComponents>();
    private Dictionary<int, Vector3> targetPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, PythonBridge.Person> lastPersonData = new Dictionary<int, PythonBridge.Person>();

    private void Start()
    {
        if (pythonBridge == null)
            pythonBridge = GetComponent<PythonBridge>();

        if (gridOrigin == null)
        {
            Debug.LogError("❌ gridOrigin이 설정되지 않았습니다!");
            gridOrigin = transform;
        }

        if (pythonBridge != null)
        {
            pythonBridge.SnapshotReceivedEvent += OnSnapshotReceived;
        }
    }

    private void OnSnapshotReceived(PythonBridge.StepSnapshot snapshot)
    {
        if (snapshot == null || snapshot.people == null)
            return;

        var currentAgentIds = new HashSet<int>();

        foreach (var person in snapshot.people)
        {
            currentAgentIds.Add(person.id);

            Vector3 targetPos = GridToWorldPosition(person.row, person.col);

            // 생성 로직
            if (!activeAgents.ContainsKey(person.id))
            {
                CreateAgentGameObject(person.id, targetPos); 
            }

            targetPositions[person.id] = targetPos;
            lastPersonData[person.id] = person; // 패닉, 속도 데이터 갱신
        }

        // 죽거나 탈출한 에이전트 파괴 처리
        var deadAgents = new List<int>();
        foreach (var id in activeAgents.Keys)
        {
            if (!currentAgentIds.Contains(id))
                deadAgents.Add(id);
        }

        foreach (var id in deadAgents)
        {
            if (activeAgents.TryGetValue(id, out var agentComp))
            {
                Destroy(agentComp.go);
                activeAgents.Remove(id);
                targetPositions.Remove(id);
                lastPersonData.Remove(id);
            }
        }
    }

    private void Update()
    {
        foreach (var kvp in activeAgents)
        {
            int agentId = kvp.Key;
            AgentComponents agent = kvp.Value;

            if (agent.go == null) continue;
            if (!lastPersonData.TryGetValue(agentId, out var personData)) continue;

            float currentSpeed = personData.speed;
            float currentPanic = personData.panic;

            // ==========================================
            // 1. 시각화 업데이트 (절차적 애니메이션 & 공황)
            // ==========================================
            
            // 💡 병목 현상 연출: 속도에 따라 ProceduralHumanAnimator 수치 조절
            if (agent.procAnimator != null)
            {
                if (currentSpeed < 0.6f) // 사람이 몰려서 속도가 줄어들었을 때 (수치는 조절 가능)
                {
                    // 좁은 보폭으로 발을 빨리 구름 (발동동)
                    agent.procAnimator.WalkSwingAngle = Mathf.Lerp(agent.procAnimator.WalkSwingAngle, 10f, Time.deltaTime * 5f);
                    agent.procAnimator.WalkCycleSpeed = Mathf.Lerp(agent.procAnimator.WalkCycleSpeed, 5.0f, Time.deltaTime * 5f);
                }
                else
                {
                    // 정상 걷기 상태 (스크린샷에 있던 기본값)
                    agent.procAnimator.WalkSwingAngle = Mathf.Lerp(agent.procAnimator.WalkSwingAngle, 28f, Time.deltaTime * 5f);
                    agent.procAnimator.WalkCycleSpeed = Mathf.Lerp(agent.procAnimator.WalkCycleSpeed, 2.8f, Time.deltaTime * 5f);
                }
            }

            // 패닉 상태에 따른 머터리얼 색상 변화 (하얗게 -> 붉게)
            if (agent.renderer != null)
            {
                agent.renderer.material.color = Color.Lerp(normalColor, panicColor, currentPanic);
            }

            // 패닉 임계치 초과 시 머리 위 땀방울 파티클 재생
            if (agent.sweatParticle != null)
            {
                if (currentPanic >= panicThresholdForSweat && !agent.sweatParticle.isPlaying)
                    agent.sweatParticle.Play();
                else if (currentPanic < panicThresholdForSweat && agent.sweatParticle.isPlaying)
                    agent.sweatParticle.Stop();
            }

            // ==========================================
            // 2. 이동 및 회전 업데이트
            // ==========================================
            if (targetPositions.TryGetValue(agentId, out var targetPos))
            {
                if (useSmoothing)
                {
                    // 이동 거리도 파이썬 스피드에 맞춰 조절
                    float actualMoveSpeed = baseWalkSpeed * currentSpeed;
                    
                    agent.go.transform.position = Vector3.MoveTowards(
                        agent.go.transform.position,
                        targetPos,
                        actualMoveSpeed * Time.deltaTime
                    );
                }
                else
                {
                    agent.go.transform.position = targetPos;
                }

                // 목표 방향 바라보기
                Vector3 moveDirection = targetPos - agent.go.transform.position;
                moveDirection.y = 0; 
                
                if (moveDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    agent.go.transform.rotation = Quaternion.Slerp(
                        agent.go.transform.rotation, 
                        targetRotation, 
                        rotationSpeed * Time.deltaTime
                    );
                }
            }
        }
    }

    private Vector3 GridToWorldPosition(int row, int col)
    {
        Vector3 worldPos = gridOrigin.position 
            + gridOrigin.right * col * cellSize 
            + gridOrigin.forward * -row * cellSize; 
        
        worldPos.y = gridOrigin.position.y + agentHeight;
        return worldPos;
    }

    private void CreateAgentGameObject(int agentId, Vector3 spawnPos)
    {
        GameObject agentGO;

        if (agentPrefab != null)
        {
            agentGO = Instantiate(agentPrefab, spawnPos, Quaternion.identity);
            agentGO.name = $"Agent_{agentId}";
        }
        else
        {
            // 프리팹이 없을 경우를 대비한 기본 캡슐 생성 (테스트용)
            agentGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            agentGO.name = $"Agent_{agentId}";
            agentGO.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            agentGO.transform.position = spawnPos;

            Collider collider = agentGO.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        agentGO.layer = LayerMask.NameToLayer("Default");

        // 💡 3. 컴포넌트 캐싱 및 저장
        AgentComponents newAgentComp = new AgentComponents { go = agentGO };
        
        // ProceduralHumanAnimator 컴포넌트 가져오기 (스크린샷처럼 최상단에 있다고 가정)
        newAgentComp.procAnimator = agentGO.GetComponent<ProceduralHumanAnimator>();
        
        // 자식들 중에서 렌더러 찾기
        newAgentComp.renderer = agentGO.GetComponentInChildren<Renderer>();
        
        // 프리팹 자식 중 "SweatParticle"이라는 이름의 파티클이 있다면 땀방울 효과용으로 연결
        Transform sweatTrans = agentGO.transform.Find("SweatParticle");
        if (sweatTrans != null)
        {
            newAgentComp.sweatParticle = sweatTrans.GetComponent<ParticleSystem>();
        }

        activeAgents[agentId] = newAgentComp;
    }

    public int GetAgentCount() => activeAgents.Count;

    private void OnDestroy()
    {
        if (pythonBridge != null)
            pythonBridge.SnapshotReceivedEvent -= OnSnapshotReceived;
    }
}