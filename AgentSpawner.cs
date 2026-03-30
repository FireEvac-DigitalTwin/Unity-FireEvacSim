using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

public class AgentSpawner : MonoBehaviour
{
    public static AgentSpawner Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [Header("에이전트 설정")]
    public GameObject agentPrefab;
    public int agentCount = 10;
    public float minDistanceFromFire = 5.0f; // ⭐ 요청하신 대로 5m로 수정
    public float agentAvoidRadius = 0.5f;    // 에이전트 간/벽과의 최소 간격 (OverlapSphere 체크용)

    private List<EvacuationAgent_NEW> spawnedAgents = new List<EvacuationAgent_NEW>();

    void Start()
    {
        SpawnInitialAgents();
    }

    private void SpawnInitialAgents()
    {
        if (agentPrefab == null) return;

        Vector3 areaCenter = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.randomAreaCenter : transform.position;
        Vector3 areaSize = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.randomAreaSize : new Vector3(20, 0, 20);
        Vector3 firePos = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.LastFirePosition : Vector3.zero;

        for (int i = 0; i < agentCount; i++)
        {
            Vector3 spawnPos = GetValidRandomPosition(areaCenter, areaSize, firePos);
            GameObject go = Instantiate(agentPrefab, spawnPos, Quaternion.identity);
            
            var evac = go.GetComponent<EvacuationAgent_NEW>();
            if (evac != null)
            {
                evac.startPosition = spawnPos;
                spawnedAgents.Add(evac);
                if (EvacuationManager.Instance != null)
                    EvacuationManager.Instance.RegisterAgent(evac);
            }
        }
    }

    /// <summary>
    /// ⭐ 매 에피소드마다 호출되어 에이전트들의 '시작 위치'를 재연산합니다.
    /// 실제 이동(Warp)은 EvacuationManager에서 순서대로 처리하므로 여기서는 위치값만 바꿉니다.
    /// </summary>
    public void RespawnAll()
    {
        Vector3 areaCenter = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.randomAreaCenter : transform.position;
        Vector3 areaSize = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.randomAreaSize : new Vector3(20, 0, 20);
        Vector3 firePos = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.LastFirePosition : Vector3.zero;

        foreach (var agent in spawnedAgents)
        {
            if (agent == null) continue;
            
            // 새 에피소드를 위한 랜덤 위치 뽑기
            Vector3 newPos = GetValidRandomPosition(areaCenter, areaSize, firePos);
            
            // 에이전트의 시작 좌표만 갱신 (이동 처리는 EvacuationManager 순서에 맞춰 진행)
            agent.startPosition = newPos; 
        }
        Debug.Log($"👥 [Spawner] {spawnedAgents.Count}명의 시작 위치 재랜덤화 완료.");
    }

    private Vector3 GetValidRandomPosition(Vector3 center, Vector3 size, Vector3 firePos)
    {
        int maxAttempts = 150;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = center + new Vector3(
                Random.Range(-size.x * 0.5f, size.x * 0.5f),
                0f,
                Random.Range(-size.z * 0.5f, size.z * 0.5f)
            );

            // 1. ⭐ 출구 구역 제외 (요청하신 x:10~80, z:-60~-40 범위로 수정)
            if (candidate.x >= 10f && candidate.x <= 80f && candidate.z >= -60f && candidate.z <= -40f)
                continue;

            // 2. NavMesh 확인
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, 2.0f, NavMesh.AllAreas)) continue;
            candidate = hit.position;

            // 3. ⭐ 벽 겹침 체크 (OverlapSphere 0.5f 사용)
            bool nearWall = false;
            Collider[] hitColliders = Physics.OverlapSphere(candidate, agentAvoidRadius);
            foreach (var col in hitColliders)
            {
                if (col.CompareTag("Wall") || col.CompareTag("Agent"))
                {
                    nearWall = true;
                    break;
                }
            }
            if (nearWall) continue;

            // 4. ⭐ 초기 불 위치에서 5m 이상 떨어진 곳만 허용
            if (Vector3.Distance(candidate, firePos) < minDistanceFromFire) continue;

            return candidate;
        }
        
        Debug.LogWarning("⚠️ 유효한 스폰 위치를 찾지 못해 기본 중심값을 반환합니다.");
        return center;
    }
}
