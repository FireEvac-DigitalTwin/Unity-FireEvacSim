using UnityEngine;

/// <summary>
/// 방 안 스폰 위치를 표시하는 마커. RoomDetectorWindow가 자동 생성합니다.
/// </summary>
public class RoomSpawnPoint : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.85f, 1f, 0.8f);
        Gizmos.DrawSphere(transform.position, 0.25f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);

        Gizmos.color = new Color(0f, 0.85f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
