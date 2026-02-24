using UnityEngine;
using UnityEngine.AI;

public class AgentMover : MonoBehaviour
{
    public NavMeshAgent agent;

    [Header("Destination")]
    [Tooltip("출구 또는 목표 지점. 비어 있으면 같은 오브젝트의 EvacuationAgent_NEW.exitPoint 사용")]
    public Transform target;

    [Header("Fire Avoidance")]
    [SerializeField] private float fireCheckInterval = 0.5f; // 불 체크 주기 (성능 최적화)
    private float nextCheckTime;

    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        RefreshTarget();
        SetDestinationIfReady();
    }

    void Update()
    {
        if (agent == null) return;

        RefreshTarget();
        if (target == null) return;

        // --- 1. 불(Fire) 감지 및 회피 로직 추가 ---
        if (Time.time >= nextCheckTime)
        {
            CheckFireOnPath();
            nextCheckTime = Time.time + fireCheckInterval;
        }

        // --- 2. 경로 유지 로직 ---
        if (!agent.isStopped && !agent.hasPath)
            SetDestinationIfReady();

        // 목적지 도착 시 정지
        if (!agent.pathPending && agent.remainingDistance < 0.3f)
            agent.isStopped = true;
    }

    void CheckFireOnPath()
    {
        // 에이전트의 현재 위치가 불 위인지 확인
        if (FireSpreadManager.Instance != null && FireSpreadManager.Instance.IsNodeOnFire(transform.position))
        {
            Debug.LogWarning($"🔥 {gameObject.name}: 경로상에 불 감지! 정지합니다.");
            agent.isStopped = true;
            
            // TIP: 여기서 다시 경로를 찾거나(SetDestination) 
            // 강화학습 에이전트에게 '위험함' 점수를 줄 수 있습니다.
        }
    }

    void RefreshTarget()
    {
        if (target != null) return;
        var evacAgent = GetComponent<EvacuationAgent_NEW>();
        if (evacAgent != null && evacAgent.exitPoint != null)
            target = evacAgent.exitPoint;
    }

    void SetDestinationIfReady()
    {
        if (agent == null || target == null) return;
        var evac = GetComponent<EvacuationAgent_NEW>();
        
        // 사망했거나 이미 탈출했다면 이동하지 않음
        if (evac != null && (evac.hasEvacuated || evac.isDead)) 
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    public void ResetDestination()
    {
        RefreshTarget();
        SetDestinationIfReady();
    }
}