using UnityEngine;
using System.Collections.Generic;

public class EvacuationManager : MonoBehaviour
{
    public static EvacuationManager Instance;

    [Header("Evacuation Agents (People)")]
    public List<EvacuationAgent_NEW> agents;

    [Header("RL Agent")]
    public EvacGuidanceAgent guidanceAgent;

    [Header("Episode Settings")]
    public float maxEpisodeTime = 60f;

    private float episodeTimer = 0f;
    private bool episodeEnded = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        StartEpisode();
    }

    void Update()
    {
        if (episodeEnded) return;

        episodeTimer += Time.deltaTime;

        bool anyDead = false;
        bool allDone = true;
        bool anyEvacuated = false;

        float totalEvacTime = 0f;
        int notEvacuatedCount = 0;

        foreach (var a in agents)
        {
            if (a.isDead)
                anyDead = true;

            if (a.hasEvacuated)
            {
                totalEvacTime += a.evacTime;
                anyEvacuated = true;
            }
            else if (!a.isDead)
            {
                allDone = false;
                notEvacuatedCount++;
            }
        }

        // ✅ Episode 종료 조건 (강화학습 핵심)
        // - 에이전트 중 하나라도 탈출했거나(anyEvacuated)
        // - 사망자가 있거나(anyDead)
        // - 최대 시간 초과 시
        if (anyDead || anyEvacuated || episodeTimer >= maxEpisodeTime)
        {
            episodeEnded = true;
            StopAllAgents();

            // 🔥 불 확산 정지
            if (FireSpreadManager.Instance != null)
                FireSpreadManager.Instance.StopSpread();

            Debug.Log($"[EvacuationManager] Episode Ended | anyDead={anyDead}, anyEvacuated={anyEvacuated}, allDone={allDone}, time={episodeTimer:F1}");

            float avgTime =
                agents.Count > 0 ? totalEvacTime / agents.Count : 0f;

            if (guidanceAgent != null)
                guidanceAgent.EndEpisodeWithResult(avgTime, notEvacuatedCount);

            Invoke(nameof(StartEpisode), 0.5f);
        }
    }

    void StartEpisode()
    {
        episodeEnded = false;
        episodeTimer = 0f;

        foreach (var a in agents)
            a.ResetAgent();

        // 🔥 불/연기 리셋 (있으면)
        if (FireSpreadManager.Instance != null)
            FireSpreadManager.Instance.ResetFire();

        Debug.Log($"[EvacuationManager] StartEpisode | agents={agents.Count}");

        // FireManager.Instance.ResetFire(); // 별도 화재 매니저 사용 시
    }

    /// <summary> 에피소드 종료 시 모든 에이전트 이동 정지 </summary>
    void StopAllAgents()
    {
        foreach (var a in agents)
        {
            if (a == null) continue;
            var nav = a.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null)
                nav.isStopped = true;
        }
    }
}
