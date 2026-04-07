using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EvacuationAgent_NEW : MonoBehaviour
{
    [Header("Evacuation State")]
    public bool hasEvacuated = false;
    public bool isDead = false;          // ⭐ 추가
    public float evacTime = 0f;

    [Header("Exit")]
    public Transform exitPoint;
    public float exitDistance = 30f; // 출구 범위 늘림 (병목 해결)

    [Header("Death Effect")]
    [Tooltip("사망 시 재생할 이펙트 (선택 사항)")]
    public GameObject deathEffectPrefab;
    [Tooltip("사망 시 몸 색을 어둡게(그을린 느낌) 만들지 여부")]
    public bool darkenOnDeath = true;
    [Tooltip("사망 후 에이전트를 비활성화할지 여부")]
    public bool disableOnDeath = true;
    [Tooltip("사망 후 에이전트를 비활성화하기까지의 지연 시간(초)")]
    public float deathDisableDelay = 1.5f;

    [Header("Exit Waiting")]
    public float exitWaitTime = 1f; // 출구 도착 후 기다릴 시간
    public int maxAgentsAtExit = 3; // 출구 근처 최대 에이전트 수 (혼잡 시 다른 출구로)

    public Vector3 startPosition;
    private NavMeshAgent navAgent;
    private float exitWaitTimer = 0f;
    private bool isWaitingAtExit = false;
    private Transform currentExitPoint;

    void Awake()
    {
        startPosition = transform.position;
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // 런타임 생성(Spawner)일 경우 exitPoint가 비어있을 수 있어 자동 할당
        EnsureExitPoint();
    }

    public bool EnsureExitPoint(Transform exclude = null)
    {
        if (exitPoint != null) return true;

        GameObject[] exits;
        try
        {
            exits = GameObject.FindGameObjectsWithTag("Exit");
        }
        catch
        {
            exits = null;
        }

        if (exits == null || exits.Length == 0) return false;

        Transform best = null;
        float bestScore = float.MaxValue; // 낮을수록 좋음 (거리 + 혼잡 + 불 거리)

        foreach (var go in exits)
        {
            if (go == null || go.transform == exclude) continue;

            float dist = Vector3.Distance(transform.position, go.transform.position);

            // 혼잡도: 출구 근처 에이전트 수
            int crowd = CountAgentsNearExit(go.transform);

            // 불 거리: 출구에서 가장 가까운 불까지의 거리 (낮을수록 위험)
            float fireDist = GetDistanceToNearestFire(go.transform.position);
            if (fireDist < 5f) continue; // 불과 너무 가까우면 제외

            // 점수: 거리 + 혼잡 * 10 + (10 - fireDist) * 5 (불 가까울수록 패널티)
            float score = dist + crowd * 10f + Mathf.Max(0, 10f - fireDist) * 5f;

            if (score < bestScore)
            {
                bestScore = score;
                best = go.transform;
            }
        }

        exitPoint = best;
        if (exitPoint != null)
            Debug.Log($"[EvacuationAgent_NEW] {gameObject.name} exitPoint 자동 할당: {exitPoint.name} (exclude: {exclude?.name})");

        return exitPoint != null;
    }

    void Update()
    {
        if (hasEvacuated || isDead) return;
        if (exitPoint == null) 
        {
            EnsureExitPoint();
            return;
        }

        float dist = Vector3.Distance(transform.position, exitPoint.position);
        if (dist <= exitDistance)
        {
            // 출구 도착 시 바로 탈출 처리 (병목 방지)
            CompleteEvacuation();
        }
        else
        {
            // 출구에서 멀어지면 대기 취소
            if (isWaitingAtExit)
            {
                isWaitingAtExit = false;
                if (navAgent != null) navAgent.isStopped = false;
            }
        }
    }

    // =====================
    // 탈출 성공
    // =====================
    void CompleteEvacuation()
    {
        hasEvacuated = true;
        evacTime = Time.timeSinceLevelLoad;

        if (navAgent != null)
            navAgent.isStopped = true;

        Debug.Log($"[EvacuationAgent_NEW] {gameObject.name} 탈출 완료 (time={evacTime:F2})");

        if (GuidanceManager.Instance != null)
            GuidanceManager.Instance.RewardForEvacuation();

        // 맵 밖으로 나간 것으로 간주하고 씬 안에서는 비활성화
        gameObject.SetActive(false);
    }

    // =====================
    // 🔥 사망 처리 (불에 닿았을 때 호출)
    // =====================
    public void Die()
    {
        if (hasEvacuated || isDead) return;

        isDead = true;

        if (navAgent != null)
            navAgent.isStopped = true;

        if (GuidanceManager.Instance != null)
            GuidanceManager.Instance.PenaltyForDeath();

        // 이펙트 생성
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // 몸을 어둡게 처리해서 '타 죽은' 느낌 주기
        if (darkenOnDeath)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    m.color = Color.black * 0.7f;
                    if (m.HasProperty("_BaseColor"))
                        m.SetColor("_BaseColor", Color.black * 0.7f);
                }
            }
        }

        if (disableOnDeath)
            StartCoroutine(DisableAfterDelay(deathDisableDelay));
    }

    // =====================
    // Episode 리셋
    // =====================
    public void ResetAgent()
    {
        StopAllCoroutines();
        hasEvacuated = false;
        isDead = false;
        evacTime = 0f;

        gameObject.SetActive(true);

        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(startPosition); // NavMesh 위에 올바르게 재배치
            navAgent.ResetPath();
            navAgent.isStopped = true;
        }

        var arrowFollower = GetComponent<AgentArrowFollower>();
        if (arrowFollower != null)
            arrowFollower.ResetFollower();

        var mover = GetComponent<AgentMover>();
        if (mover != null)
            mover.ResetDestination();
    }

    IEnumerator DisableAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        gameObject.SetActive(false);
    }

    public void Evacuate()
    {
        if (hasEvacuated) return;

        hasEvacuated = true;
        evacTime = Time.time;

        Debug.Log($"✅ {gameObject.name} 탈출 성공! 시간: {evacTime:F2}");
    }

    private int CountAgentsNearExit(Transform exit)
    {
        int count = 0;
        if (EvacuationManager.Instance == null) return 0;

        foreach (var agent in EvacuationManager.Instance.agents)
        {
            if (agent != null && agent != this && Vector3.Distance(agent.transform.position, exit.position) <= exitDistance)
            {
                count++;
            }
        }
        return count;
    }

    private float GetDistanceToNearestFire(Vector3 pos)
    {
        if (FireSpreadManager.Instance == null) return float.MaxValue;

        float minDist = float.MaxValue;
        foreach (var firePos in FireSpreadManager.Instance.GetFirePositions())
        {
            float dist = Vector3.Distance(pos, firePos);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }
}

void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Exit"))
    {
        CompleteEvacuation();
    }
}
