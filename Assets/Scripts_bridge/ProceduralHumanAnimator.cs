using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 실제 이동 속도를 기반으로 팔/다리 피벗을 흔들어 걷기/뛰기를 표현합니다.
/// AgentSynchronizer의 강제 이동(Transform)과 NavMeshAgent 모두 완벽히 호환됩니다.
/// </summary>
public class ProceduralHumanAnimator : MonoBehaviour
{
    [Header("Limb Pivots (HumanoidBuilderWindow에서 자동 할당)")]
    public Transform leftLegPivot;
    public Transform rightLegPivot;
    public Transform leftArmPivot;
    public Transform rightArmPivot;

    [Header("Walk")]
    public float walkSwingAngle = 28f;
    public float walkCycleSpeed = 2.8f;

    [Header("Run")]
    public float runSwingAngle  = 50f;
    public float runCycleSpeed  = 5.5f;
    [Tooltip("이 속도(m/s) 이상이면 뛰는 애니메이션")]
    public float runSpeedThreshold = 2.5f;

    // 💡 AgentSynchronizer 스크립트와의 호환성을 위한 C# 프로퍼티 추가
    public float WalkSwingAngle { get => walkSwingAngle; set => walkSwingAngle = value; }
    public float WalkCycleSpeed { get => walkCycleSpeed; set => walkCycleSpeed = value; }

    private NavMeshAgent navAgent;
    private float cyclePhase;
    private Vector3 lastPosition; // 💡 실제 이동량 계산용

    void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        lastPosition = transform.position;
    }

    void Update()
    {
        float speed = 0f;

        // 1. NavMeshAgent가 스스로 길찾기를 하며 이동 중일 때
        if (navAgent != null && navAgent.enabled && navAgent.velocity.sqrMagnitude > 0.01f)
        {
            speed = navAgent.velocity.magnitude;
        }
        // 2. 💡 AgentSynchronizer(파이썬)가 transform.position을 강제로 움직일 때
        else
        {
            Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
            velocity.y = 0f; // 상하 이동(Y축)은 걷기 속도 계산에서 제외
            speed = velocity.magnitude;
        }

        // 다음 프레임 계산을 위해 현재 위치 저장
        lastPosition = transform.position;

        if (speed < 0.1f)
        {
            // 멈췄을 때 자연스럽게 idle 자세로 복귀
            float lerpSpeed = Time.deltaTime * 6f;
            BlendToIdle(lerpSpeed);
            cyclePhase = 0f;
            return;
        }

        bool running = speed >= runSpeedThreshold;
        
        // 💡 외부(AgentSynchronizer)에서 병목 현상으로 walkCycleSpeed 등을 변경하면 즉각 반영됨
        cyclePhase += Time.deltaTime * (running ? runCycleSpeed : walkCycleSpeed);

        float leg = Mathf.Sin(cyclePhase) * (running ? runSwingAngle : walkSwingAngle);
        SetPivot(leftLegPivot,   leg);
        SetPivot(rightLegPivot, -leg);
        SetPivot(leftArmPivot,  -leg * 0.5f);
        SetPivot(rightArmPivot,  leg * 0.5f);
    }

    void SetPivot(Transform pivot, float xAngle)
    {
        if (pivot == null) return;
        pivot.localRotation = Quaternion.Euler(xAngle, 0f, 0f);
    }

    void BlendToIdle(float t)
    {
        foreach (var p in new[] { leftLegPivot, rightLegPivot, leftArmPivot, rightArmPivot })
        {
            if (p == null) continue;
            p.localRotation = Quaternion.Slerp(p.localRotation, Quaternion.identity, t);
        }
    }
}