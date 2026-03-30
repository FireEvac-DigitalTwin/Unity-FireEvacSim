using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EvacuationAgent_NEW : MonoBehaviour
{
    [Header("Evacuation State")]
    public bool hasEvacuated = false;
    public bool isDead = false;
    public float evacTime = 0f;
    [Tooltip("개별 에이전트의 대피 시작 시각. AgentMover.evacuationStarted가 true가 된 순간 기록됩니다.")]
    public float evacStartTime = -1f;
    [Tooltip("사망 시각. 사망하지 않으면 -1.")]
    public float deathTime = -1f;

    [Header("Exit")]
    public Transform exitPoint;
    public float exitDistance = 2f;

    [Header("Death Effect")]
    public GameObject deathEffectPrefab;
    public bool darkenOnDeath = true;
    public bool disableOnDeath = true;
    public float deathDisableDelay = 1.5f;

    // 외부(AgentSpawner)에서 위치를 설정할 수 있도록 public 유지
    public Vector3 startPosition;
    private NavMeshAgent navAgent;

    void Awake()
    {
        startPosition = transform.position;
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // ⭐ [병목 완화] 에이전트 간 우선순위 랜덤 부여 (0: 최고, 99: 최저)
        // 모든 에이전트가 같은 우선순위일 때 발생하는 '서로 밀치기' 현상을 방지합니다.
        if (navAgent != null)
        {
            navAgent.avoidancePriority = Random.Range(0, 100);
        }

        // 런타임 생성 시 출구 자동 할당
        EnsureExitPoint();
    }

    public bool EnsureExitPoint()
    {
        if (exitPoint != null) return true;

        GameObject[] exits;
        try { exits = GameObject.FindGameObjectsWithTag("Exit"); }
        catch { exits = null; }

        if (exits == null || exits.Length == 0) return false;

        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var go in exits)
        {
            if (go == null) continue;
            float d = (go.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = go.transform;
            }
        }

        exitPoint = best;
        return exitPoint != null;
    }

    void Update()
    {
        if (hasEvacuated || isDead) return;

        var mover = GetComponent<AgentMover>();
        if (mover != null && mover.evacuationStarted && evacStartTime < 0f)
            evacStartTime = Time.timeSinceLevelLoad;

        if (exitPoint == null) return;

        float dist = Vector3.Distance(transform.position, exitPoint.position);
        if (dist <= exitDistance)
        {
            Evacuate(); // 탈출 메서드 호출
        }
    }

    // =====================
    // ⭐ [수정] 탈출 처리 로직 통합
    // =====================
    public void Evacuate()
    {
        if (hasEvacuated || isDead) return;

        hasEvacuated = true;
        float end = Time.timeSinceLevelLoad;
        float start = evacStartTime >= 0f ? evacStartTime : 0f;
        evacTime = Mathf.Max(0f, end - start);

        if (navAgent != null)
            navAgent.isStopped = true;

        Debug.Log($"✅ {gameObject.name} 탈출 성공! 시간: {evacTime:F2}");

        // 씬에서 비활성화
        gameObject.SetActive(false);
    }

    // =====================
    // 🔥 사망 처리
    // =====================
    public void Die()
    {
        if (hasEvacuated || isDead) return;

        isDead = true;
        deathTime = Time.timeSinceLevelLoad;

        if (navAgent != null)
            navAgent.isStopped = true;

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        if (darkenOnDeath)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    m.color = Color.black * 0.7f;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black * 0.7f);
                }
            }
        }

        if (disableOnDeath)
            StartCoroutine(DisableAfterDelay(deathDisableDelay));
    }

    // =====================
    // 🔄 Episode 리셋 (Warp 적용)
    // =====================
    public void ResetAgent()
    {
        gameObject.SetActive(true);
        hasEvacuated = false;
        isDead = false;
        evacTime = 0f;
        evacStartTime = -1f;
        deathTime = -1f;

        if (navAgent != null)
        {
            // ⭐ NavMeshAgent를 새 위치로 강제 이동 (순간이동)
            navAgent.Warp(startPosition); 
            navAgent.isStopped = false;
            navAgent.ResetPath();
            
            // 리셋할 때마다 우선순위를 다시 부여하여 다양한 상황 연출 가능
            navAgent.avoidancePriority = Random.Range(0, 100);
        }
        else
        {
            transform.position = startPosition;
        }

        var mover = GetComponent<AgentMover>();
        if (mover != null)
            mover.ResetDestination();
    }

    IEnumerator DisableAfterDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (isDead) gameObject.SetActive(false);
    }
}