using UnityEngine;

public class FireDamage : MonoBehaviour
{
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
