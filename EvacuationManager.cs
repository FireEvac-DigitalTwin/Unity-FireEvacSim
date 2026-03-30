using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class EvacuationManager : MonoBehaviour
{
    public static EvacuationManager Instance;

    [Header("Agents (모든 대피자를 여기에 등록)")]
    public List<EvacuationAgent_NEW> agents = new List<EvacuationAgent_NEW>();

    [Header("Episode Settings")]
    public float maxEpisodeTime = 240f;

    [Header("Batch Experiment (A* baseline 등)")]
    [Tooltip("true면 지정한 횟수만큼 에피소드를 자동 반복하고, 마지막에 평균/분산(표준편차)을 한 번에 출력합니다.")]
    public bool runBatchExperiments = false;

    [Tooltip("반복 실행할 에피소드 수 (예: 30)")]
    public int batchEpisodeCount = 30;

    [Tooltip("에피소드 사이 대기 시간(초). 리셋/오브젝트 활성화 반영용")]
    public float interEpisodeDelay = 0.25f;

    [Header("Batch Export")]
    [Tooltip("배치 실험이 끝나면 CSV로 저장합니다. 경로는 Application.persistentDataPath 입니다.")]
    public bool exportBatchCsv = true;

    [Tooltip("CSV 파일명 접두어")]
    public string batchCsvFilePrefix = "AStarBaseline";

    [Header("End Behavior")]
    [Tooltip("모든 에이전트가 탈출/사망하면 즉시 에피소드를 끝냅니다.")]
    public bool endImmediatelyWhenAllAgentsDone = true;

    [Tooltip("에피소드가 끝나면 플레이 모드를 자동 종료합니다(에디터에서만).")]
    public bool stopPlayModeOnEpisodeEnd = false;

    private float episodeTimer = 0f;
    private bool episodeEnded = false;

    // 배치 실험 상태
    private int batchEpisodeIndex = 0; // 1-based로 출력
    private List<EpisodeResult> batchResults = new List<EpisodeResult>();
    private RunningStats avgEvacStats = new RunningStats();
    private RunningStats deathRateStats = new RunningStats();

    struct EpisodeResult
    {
        public int index;
        public int totalAgents;
        public int evacuated;
        public int dead;
        public int aliveTimeout;
        public float avgEvacTime;
        public float episodeTime;
        public float deathRate;
    }

    struct RunningStats
    {
        public int n;
        public double mean;
        public double m2;

        public void Clear()
        {
            n = 0; mean = 0; m2 = 0;
        }

        public void Push(double x)
        {
            n++;
            double delta = x - mean;
            mean += delta / n;
            double delta2 = x - mean;
            m2 += delta * delta2;
        }

        public double VarianceSample => n > 1 ? (m2 / (n - 1)) : 0.0;
        public double StdDevSample => System.Math.Sqrt(VarianceSample);
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        RefreshAgentsFromScene();

        if (runBatchExperiments)
        {
            int target = Mathf.Max(1, batchEpisodeCount);
            Debug.Log($"[Batch] 활성화됨. targetEpisodes={target}, exportCsv={exportBatchCsv}, stopPlayOnEnd={stopPlayModeOnEpisodeEnd}");
            batchEpisodeIndex = 0;
            batchResults.Clear();
            avgEvacStats.Clear();
            deathRateStats.Clear();
        }

        StartEpisode();
    }

    void Update()
    {
        if (episodeEnded) return;

        episodeTimer += Time.deltaTime;

        // 런타임 생성(Spawner) 등으로 에이전트가 늘어날 수 있어 주기적으로 보강
        if (agents.Count == 0)
            RefreshAgentsFromScene();

        bool allDone = AllAgentsDone();

        // 모든 에이전트가 죽거나 탈출했는지 확인
        if ((endImmediatelyWhenAllAgentsDone && allDone) || episodeTimer >= maxEpisodeTime)
        {
            EndEpisode();
        }
    }

    /// <summary>모든 에이전트가 죽었거나 탈출했으면 true</summary>
    bool AllAgentsDone()
    {
        foreach (var agent in agents)
        {
            if (agent == null) continue;
            if (!agent.isDead && !agent.hasEvacuated)
                return false; // 아직 살아있고 탈출 안 한 에이전트가 있음
        }
        return true;
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
                Debug.Log($"[Agent] {agent.gameObject.name} : EVACUATED (t={agent.evacTime:F2}, start={agent.evacStartTime:F2})");
            }
            else if (agent.isDead)
            {
                dead++;
                Debug.Log($"[Agent] {agent.gameObject.name} : DEAD (start={agent.evacStartTime:F2}, death={agent.deathTime:F2})");
            }
            else
            {
                alive++; // 시간 초과로 아직 살아있는 에이전트
                Debug.Log($"[Agent] {agent.gameObject.name} : ALIVE (timeout, start={agent.evacStartTime:F2})");
            }
        }

        float avgEvacTime = evacuated > 0 ? totalEvacTime / evacuated : 0f;

        // 배치 결과 저장
        if (runBatchExperiments)
        {
            batchEpisodeIndex = Mathf.Max(1, batchEpisodeIndex);
            float deathRate = agents.Count > 0 ? (float)dead / agents.Count : 0f;

            var er = new EpisodeResult
            {
                index = batchEpisodeIndex,
                totalAgents = agents.Count,
                evacuated = evacuated,
                dead = dead,
                aliveTimeout = alive,
                avgEvacTime = avgEvacTime,
                episodeTime = episodeTimer,
                deathRate = deathRate
            };
            batchResults.Add(er);
            avgEvacStats.Push(avgEvacTime);
            deathRateStats.Push(deathRate);
        }

        Debug.Log("========== Episode Result ==========");
        Debug.Log($"Total Agents: {agents.Count}");
        Debug.Log($"Evacuated: {evacuated}");
        Debug.Log($"Dead: {dead}");
        Debug.Log($"Alive (timeout): {alive}");
        Debug.Log($"Survival Rate: {(float)(evacuated + alive) / agents.Count:P0}");
        Debug.Log($"Avg Evacuation Time: {avgEvacTime:F2}");
        Debug.Log($"Episode Time: {episodeTimer:F2}");
        Debug.Log("====================================");

#if UNITY_EDITOR
        // 배치 모드에서는 CSV 저장/요약 출력 이후에만 플레이 종료를 처리합니다.
        if (!runBatchExperiments)
        {
            if (stopPlayModeOnEpisodeEnd && UnityEditor.EditorApplication.isPlaying)
                UnityEditor.EditorApplication.isPlaying = false;
        }
#endif

        // 다음 에피소드(배치)로 자동 진행
        if (runBatchExperiments)
        {
            int target = Mathf.Max(1, batchEpisodeCount);
            if (batchEpisodeIndex >= target)
            {
                PrintBatchSummary();
                if (exportBatchCsv)
                    SaveBatchCsv();

#if UNITY_EDITOR
                if (stopPlayModeOnEpisodeEnd && UnityEditor.EditorApplication.isPlaying)
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                return;
            }

            StartCoroutine(BeginNextEpisodeAfterDelay(interEpisodeDelay));
        }
    }

    IEnumerator BeginNextEpisodeAfterDelay(float delay)
    {
        Debug.Log($"[Batch] 다음 에피소드 예약: {delay:F2}s 후 시작");
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        StartEpisode();
    }

    void StartEpisode()
    {
        episodeEnded = false;
        episodeTimer = 0f;

        RefreshAgentsFromScene();
        foreach (var agent in agents)
        {
            if (agent != null)
            {
                // 이전 에피소드에서 비활성화된 에이전트(탈출/사망)를 다시 활성화
                if (!agent.gameObject.activeSelf)
                    agent.gameObject.SetActive(true);
                agent.ResetAgent();
            }
        }

        if (FireSpreadManager.Instance != null)
            FireSpreadManager.Instance.ResetFire();

        // 배치 실험: 에피소드 인덱스 증가(1-based)
        if (runBatchExperiments)
        {
            batchEpisodeIndex++;
            int target = Mathf.Max(1, batchEpisodeCount);
            Debug.Log($"[Batch] Episode {batchEpisodeIndex}/{target} 시작");
        }

        Debug.Log($"Episode Started ({agents.Count} agents)");
    }

    void StopAllAgents()
    {
        foreach (var agent in agents)
        {
            if (agent == null) continue;
            var nav = agent.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null)
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
        var found = FindObjectsOfType<EvacuationAgent_NEW>();
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

    void PrintBatchSummary()
    {
        int n = batchResults.Count;
        if (n == 0)
        {
            Debug.Log("[Batch] 결과가 없습니다.");
            return;
        }

        Debug.Log("========== Batch Experiment Summary ==========");
        Debug.Log($"Episodes: {n}");
        Debug.Log($"AvgEvacTime mean={avgEvacStats.mean:F2}, std={avgEvacStats.StdDevSample:F2}, var={avgEvacStats.VarianceSample:F2}");
        Debug.Log($"DeathRate  mean={deathRateStats.mean:P2}, std={deathRateStats.StdDevSample:P2}, var={deathRateStats.VarianceSample:F6}");
        Debug.Log("----- Per Episode Table -----");
        Debug.Log("Ep | Total | Evac | Dead | Alive(T/O) | AvgEvacTime | DeathRate | EpisodeTime");
        foreach (var r in batchResults)
        {
            Debug.Log($"{r.index,2} | {r.totalAgents,5} | {r.evacuated,4} | {r.dead,4} | {r.aliveTimeout,9} | {r.avgEvacTime,10:F2} | {r.deathRate,9:P2} | {r.episodeTime,10:F2}");
        }
        Debug.Log("==============================================");
    }

    void SaveBatchCsv()
    {
        int n = batchResults.Count;
        if (n == 0)
        {
            Debug.LogWarning("[BatchCSV] 저장할 결과가 없습니다.");
            return;
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string safePrefix = string.IsNullOrWhiteSpace(batchCsvFilePrefix) ? "BatchResult" : batchCsvFilePrefix.Trim();
        string fileName = $"{safePrefix}_{timestamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        var sb = new StringBuilder(16 * 1024);

        // Summary
        sb.AppendLine("metric,mean,std_sample,var_sample");
        sb.AppendLine($"AvgEvacTime,{avgEvacStats.mean:F6},{avgEvacStats.StdDevSample:F6},{avgEvacStats.VarianceSample:F6}");
        sb.AppendLine($"DeathRate,{deathRateStats.mean:F6},{deathRateStats.StdDevSample:F6},{deathRateStats.VarianceSample:F6}");
        sb.AppendLine();

        // Per-episode
        sb.AppendLine("episode,total_agents,evacuated,dead,alive_timeout,avg_evac_time,death_rate,episode_time");
        foreach (var r in batchResults)
        {
            sb.Append(r.index).Append(',')
              .Append(r.totalAgents).Append(',')
              .Append(r.evacuated).Append(',')
              .Append(r.dead).Append(',')
              .Append(r.aliveTimeout).Append(',')
              .Append(r.avgEvacTime.ToString("F6")).Append(',')
              .Append(r.deathRate.ToString("F6")).Append(',')
              .Append(r.episodeTime.ToString("F6"))
              .AppendLine();
        }

        try
        {
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Debug.Log($"[BatchCSV] 저장 완료: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BatchCSV] 저장 실패: {e.Message}\npath={path}");
        }
    }
}
