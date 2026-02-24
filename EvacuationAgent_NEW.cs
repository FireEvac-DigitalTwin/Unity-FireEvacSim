using UnityEngine;
using UnityEngine.AI;

public class EvacuationAgent_NEW : MonoBehaviour
{
    [Header("Evacuation State")]
    public bool hasEvacuated = false;
    public bool isDead = false;          // ⭐ 추가
    public float evacTime = 0f;

    [Header("Exit")]
    public Transform exitPoint;
    public float exitDistance = 1.2f;

    private Vector3 startPosition;
    private NavMeshAgent navAgent;

    void Awake()
    {
        startPosition = transform.position;
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (hasEvacuated || isDead) return;
        if (exitPoint == null) return;

        float dist = Vector3.Distance(transform.position, exitPoint.position);
        if (dist <= exitDistance)
        {
            CompleteEvacuation();
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
    }

    // =====================
    // Episode 리셋
    // =====================
    public void ResetAgent()
    {
        hasEvacuated = false;
        isDead = false;
        evacTime = 0f;

        transform.position = startPosition;

        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }

        var mover = GetComponent<AgentMover>();
        if (mover != null)
            mover.ResetDestination();
    }
}
