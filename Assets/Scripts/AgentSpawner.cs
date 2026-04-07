using UnityEngine;
using UnityEngine.AI;

public class AgentSpawner : MonoBehaviour
{
    public static AgentSpawner Instance { get; private set; }

    [Header("에이전트 프리팹")]
    public GameObject agentPrefab;

    [Header("생성 설정")]
    public int agentCount = 3;

    [Tooltip("불에서 최소 이 거리 이상 떨어진 곳에 에이전트 생성")]
    public float minDistanceFromFire = 5f;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (agentPrefab == null)
        {
            Debug.LogError("❌ AgentSpawner: agentPrefab이 비어 있습니다.");
            return;
        }

        for (int i = 0; i < agentCount; i++)
        {
            Vector3 spawnPos = GetRandomSpawnPosition();
            var go = Instantiate(agentPrefab, spawnPos, Quaternion.identity);

            var evac = go.GetComponent<EvacuationAgent_NEW>();
            if (evac != null)
            {
                evac.startPosition = spawnPos;
                if (EvacuationManager.Instance != null)
                    EvacuationManager.Instance.RegisterAgent(evac);
            }
        }
    }

    /// <summary>매 에피소드마다 모든 에이전트의 startPosition을 새로 랜덤 배치합니다.</summary>
    public void RespawnAll()
    {
        if (EvacuationManager.Instance == null) return;

        foreach (var agent in EvacuationManager.Instance.agents)
        {
            if (agent == null) continue;
            agent.startPosition = GetRandomSpawnPosition();
        }
    }

    Vector3 GetRandomSpawnPosition()
    {
        if (FireSpreadManager.Instance == null)
        {
            Debug.LogWarning("⚠️ AgentSpawner: FireSpreadManager.Instance가 없습니다. transform.position 사용.");
            return transform.position;
        }

        Vector3 center = FireSpreadManager.Instance.randomAreaCenter;
        Vector3 size = FireSpreadManager.Instance.randomAreaSize;

        int maxAttempts = 20;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(center.x - size.x * 0.5f, center.x + size.x * 0.5f),
                center.y,
                Random.Range(center.z - size.z * 0.5f, center.z + size.z * 0.5f)
            );

            // NavMesh 체크
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, 1f, NavMesh.AllAreas))
                continue;
            candidate = hit.position;

            // 출구 제외 구역 (x:10~80, z:-60~-40)
            if (candidate.x >= 10f && candidate.x <= 80f &&
                candidate.z >= -60f && candidate.z <= -40f)
                continue;

            // 벽 겹침 체크
            bool nearWall = false;
            foreach (var col in Physics.OverlapSphere(candidate, 0.5f))
            {
                if (col.CompareTag("Wall")) { nearWall = true; break; }
            }
            if (nearWall) continue;

            // 불에서 최소 거리 체크
            if (Vector3.Distance(candidate, FireSpreadManager.Instance.LastFirePosition) < minDistanceFromFire)
                continue;

            return candidate;
        }

        Debug.LogWarning("⚠️ AgentSpawner: 유효한 위치를 찾지 못했습니다. 중심점 사용.");
        return center;
    }
}
