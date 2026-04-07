using UnityEngine;

public class FireDamage : MonoBehaviour
{
    [Header("불 범위 설정")]
    public float damageRadius = 2.0f; // 기본 1.0f에서 2.0f로 늘림

    void Awake()
    {
        var collider = GetComponent<SphereCollider>();
        if (collider != null)
        {
            collider.radius = damageRadius;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        EvacuationAgent_NEW agent =
            other.GetComponent<EvacuationAgent_NEW>();

        if (agent != null)
        {
            agent.Die();   // ⭐ 여기서 사망 처리
        }
    }
}
