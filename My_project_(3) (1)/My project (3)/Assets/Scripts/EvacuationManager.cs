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

        float totalEvacTime = 0f;
        int notEvacuatedCount = 0;

        foreach (var a in agents)
        {
            if (a.isDead)
                anyDead = true;

            if (a.hasEvacuated)
            {
                totalEvacTime += a.evacTime;
            }
            else if (!a.isDead)
            {
                allDone = false;
                notEvacuatedCount++;
            }
        }

        // ✅ Episode 종료 조건 (강화학습 핵심)
        if (anyDead || allDone || episodeTimer >= maxEpisodeTime)
        {
            episodeEnded = true;

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
        // FireManager.Instance.ResetFire();
    }
}
