using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class EvacuationManager : MonoBehaviour
{
    public static EvacuationManager Instance;

    [Header("Agents (모든 대피자를 여기에 등록)")]
    public List<EvacuationAgent_NEW> agents = new List<EvacuationAgent_NEW>();

    [Header("Episode Settings")]
    public float maxEpisodeTime = 240f;

    [Header("End Behavior")]
    [Tooltip("모든 에이전트가 탈출/사망하면 즉시 에피소드를 끝냅니다.")]
    public bool endImmediatelyWhenAllAgentsDone = true;

    [Tooltip("에피소드가 끝나면 플레이 모드를 자동 종료합니다(에디터에서만).")]
    public bool stopPlayModeOnEpisodeEnd = false;

    private float episodeTimer = 0f;
    private bool episodeEnded = false;
    private int episodeCount = 0;
    private string csvPath;
    private float totalElapsedTime = 0f;

    void Awake()
    {
        Instance = this;
        csvPath = Path.Combine(Application.dataPath, "..", "episode_results.csv");
        if (!File.Exists(csvPath))
            File.WriteAllText(csvPath, "Ep,총원,대피,사망,타임아웃,평균대피시간,사망률,에피소드시간,누적시간\n");
    }

    void Start()
    {
        RefreshAgentsFromScene();

        ResetEnvironment();
    }

    void Update()
    {
        if (episodeEnded) return;

        episodeTimer += Time.deltaTime;

        // 런타임 생성(Spawner) 등으로 에이전트가 늘어날 수 있어 주기적으로 보강
        if (agents.Count == 0)
            RefreshAgentsFromScene();

        // ML-Agents 센서 초기화 대기 (너무 빠른 EndEpisode 호출 방지)
        if (episodeTimer < 1f) return;

        bool allDone = AllAgentsDone();

        // 모든 에이전트가 죽거나 탈출했는지 확인
        if ((endImmediatelyWhenAllAgentsDone && allDone) || episodeTimer >= maxEpisodeTime)
        {
            if (episodeTimer >= maxEpisodeTime)
            {
                if (GuidanceManager.Instance != null)
                    GuidanceManager.Instance.CheckTimeout();
            }
            EndEpisode();
        }
    }

    /// <summary>모든 에이전트가 죽었거나 탈출했으면 true (에이전트가 0명이면 false)</summary>
    bool AllAgentsDone()
    {
        int valid = 0;
        foreach (var agent in agents)
        {
            if (agent == null) continue;
            valid++;
            if (!agent.isDead && !agent.hasEvacuated)
                return false; // 아직 살아있고 탈출 안 한 에이전트가 있음
        }
        return valid > 0; // 에이전트가 아무도 없으면 완료로 처리하지 않음
    }

    void EndEpisode()
    {
        episodeEnded = true;

        StopAllAgents();

        if (FireSpreadManager.Instance != null)
            FireSpreadManager.Instance.StopSpread();

        // 결과 집계
        int evacuated = 0;
        int dead = 0;
        int alive = 0;
        float totalEvacTime = 0f;

        Debug.Log("========== Agent Results ==========");
        foreach (var agent in agents)
        {
            if (agent == null) continue;
            if (agent.hasEvacuated)
            {
                evacuated++;
                totalEvacTime += agent.evacTime;
                Debug.Log($"[Agent] {agent.gameObject.name} : EVACUATED (t={agent.evacTime:F2})");
            }
            else if (agent.isDead)
            {
                dead++;
                Debug.Log($"[Agent] {agent.gameObject.name} : DEAD");
            }
            else
            {
                alive++; // 시간 초과로 아직 살아있는 에이전트
                Debug.Log($"[Agent] {agent.gameObject.name} : ALIVE (timeout)");
            }
        }

        float avgEvacTime = evacuated > 0 ? totalEvacTime / evacuated : 0f;
        totalElapsedTime += episodeTimer;
        float deathRate = agents.Count > 0 ? (float)dead / agents.Count * 100f : 0f;

        Debug.Log("========== Episode Result ==========");
        Debug.Log($"Total Agents: {agents.Count}");
        Debug.Log($"Evacuated: {evacuated}");
        Debug.Log($"Dead: {dead}");
        Debug.Log($"Alive (timeout): {alive}");
        Debug.Log($"Survival Rate: {(float)(evacuated + alive) / agents.Count:P0}");
        Debug.Log($"Avg Evacuation Time: {avgEvacTime:F2}");
        Debug.Log($"Episode Time: {episodeTimer:F2}");
        Debug.Log("====================================");

        // CSV 저장
        string line = $"{episodeCount},{agents.Count},{evacuated},{dead},{alive},{avgEvacTime:F2},{deathRate:F2},{episodeTimer:F2},{totalElapsedTime:F2}";
        File.AppendAllText(csvPath, line + "\n");
        Debug.Log($"[CSV] 저장됨: {csvPath}");

        if (GuidanceManager.Instance != null)
        {
            if (GuidanceManager.Instance.IsMLAgentsReady)
                GuidanceManager.Instance.EndEpisodeNow(); // ML-Agents 에피소드 종료
            // else: 아직 미연결 - episodeEnded=true 상태로 대기, Python 연결 시 OnEpisodeBegin이 리셋함
        }
        else
            ResetEnvironment();

#if UNITY_EDITOR
        if (stopPlayModeOnEpisodeEnd && UnityEditor.EditorApplication.isPlaying)
            UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>GuidanceManager.OnEpisodeBegin()에서 호출 - 환경만 리셋</summary>
    public void ResetEnvironment()
    {
        episodeEnded = false;
        episodeTimer = 0f;

        RefreshAgentsFromScene();

        if (FireSpreadManager.Instance != null)
            FireSpreadManager.Instance.ResetFire();

        if (AgentSpawner.Instance != null)
            AgentSpawner.Instance.RespawnAll();

        foreach (var agent in agents)
        {
            if (agent != null)
                agent.ResetAgent();
        }

        // Reset AgentMover states and restart evacuation
        foreach (var agent in agents)
        {
            var mover = agent.GetComponent<AgentMover>();
            if (mover != null)
            {
                mover.evacuationStarted = false;
                mover.evacuationEventFired = false;
                mover.RestartEvacuation();
            }
        }

        episodeCount++;
        Debug.Log($"[Episode #{episodeCount}] Started ({agents.Count} agents)");
    }

    void StopAllAgents()
    {
        foreach (var agent in agents)
        {
            if (agent == null || !agent.gameObject.activeInHierarchy) continue;
            var nav = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh)
                nav.isStopped = true;
        }
    }

    public void RegisterAgent(EvacuationAgent_NEW agent)
    {
        if (agent == null) return;
        if (!agents.Contains(agent))
            agents.Add(agent);
    }

    public void UnregisterAgent(EvacuationAgent_NEW agent)
    {
        if (agent == null) return;
        agents.Remove(agent);
    }

    void RefreshAgentsFromScene()
    {
        agents.RemoveAll(a => a == null);
        var found = FindObjectsOfType<EvacuationAgent_NEW>(true); // 비활성 오브젝트도 포함
        bool addedAny = false;
        foreach (var a in found)
        {
            if (a == null) continue;
            if (!agents.Contains(a))
            {
                agents.Add(a);
                addedAny = true;
            }
        }

        if (addedAny || agents.Count == 0)
            Debug.Log($"[EvacuationManager] 에이전트 {agents.Count}명 수집/갱신.");
    }
}
