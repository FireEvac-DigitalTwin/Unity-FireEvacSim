using UnityEngine;
using UnityEngine.AI;

public class EvacuationAgent_NEW : MonoBehaviour
{
    [Header("Evacuation State")]
    public bool hasEvacuated { get; private set; }
    public bool isDead { get; private set; }
    public float evacTime { get; private set; }

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

    // =========================
    // 탈출 성공
    // =========================
    void CompleteEvacuation()
    {
        hasEvacuated = true;
        evacTime = Time.time;

        StopAgent();
        Debug.Log($"{name} 탈출 성공");
    }

    // =========================
    // 사망 처리
    // =========================
    public void Die()
    {
        if (hasEvacuated || isDead) return;

        isDead = true;
        StopAgent();

        Debug.Log($"{name} 사망");
    }

    // =========================
    // NavMesh 정지 처리
    // =========================
    void StopAgent()
    {
        if (navAgent == null) return;

        navAgent.isStopped = true;
        navAgent.ResetPath();
    }

    // =========================
    // 에피소드 리셋
    // =========================
    public void ResetAgent()
    {
        hasEvacuated = false;
        isDead = false;
        evacTime = 0f;

        transform.position = startPosition;

        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }
    }
}