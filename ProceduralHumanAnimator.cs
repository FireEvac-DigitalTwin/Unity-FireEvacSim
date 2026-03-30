using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent 속도를 기반으로 팔/다리 피벗을 흔들어 걷기/뛰기를 표현합니다.
/// HumanoidBuilderWindow(Tools 메뉴)에서 피벗 레퍼런스를 자동 세팅합니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
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

    private NavMeshAgent navAgent;
    private float cyclePhase;

    void Awake() => navAgent = GetComponent<NavMeshAgent>();

    void Update()
    {
        float speed = navAgent != null ? navAgent.velocity.magnitude : 0f;

        if (speed < 0.1f)
        {
            // 멈췄을 때 자연스럽게 idle 자세로 복귀
            float lerpSpeed = Time.deltaTime * 6f;
            BlendToIdle(lerpSpeed);
            cyclePhase = 0f;
            return;
        }

        bool running = speed >= runSpeedThreshold;
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
