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
    public float minDistanceFromFire = 7.0f;
    public float agentAvoidRadius = 1.5f; // 에이전트 간 최소 간격

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

    public void RespawnAll()
    {
        Vector3 areaCenter = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.randomAreaCenter : transform.position;
        Vector3 areaSize = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.randomAreaSize : new Vector3(20, 0, 20);
        Vector3 firePos = FireSpreadManager.Instance != null ? FireSpreadManager.Instance.LastFirePosition : Vector3.zero;

        foreach (var agent in spawnedAgents)
        {
            if (agent == null) continue;
            Vector3 newPos = GetValidRandomPosition(areaCenter, areaSize, firePos);
            agent.startPosition = newPos;
            agent.ResetAgent(); // 에이전트 내부에서 위치를 startPosition으로 옮기는 함수 호출 필요
        }
        Debug.Log($"👥 [Spawner] {spawnedAgents.Count}명 분산 재배치 완료.");
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

            // 1. 출구 구역 제외 (FireSpreadManager와 동일 범위)
            if (candidate.x >= 5f && candidate.x <= 85f && candidate.z >= -65f && candidate.z <= -35f)
                continue;

            // 2. NavMesh 확인
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, 2.0f, NavMesh.AllAreas)) continue;
            candidate = hit.position;

            // 3. ⭐ 병목 방지: 이미 생성된 에이전트나 벽과 겹치는지 확인
            // "Agent" 태그나 "Wall" 태그가 붙은 오브젝트가 주변에 있으면 다시 뽑기
            if (Physics.CheckSphere(candidate, agentAvoidRadius, LayerMask.GetMask("Default", "Wall"))) 
                continue;

            // 4. 불과의 거리 확인
            if (Vector3.Distance(candidate, firePos) < minDistanceFromFire) continue;

            return candidate;
        }
        return center;
    }
}