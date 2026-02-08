using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FireSpreadManager : MonoBehaviour
{
    [Header("설정")]
    public GameObject firePrefab;
    public float spreadTime = 3.0f;
    public float gridSize = 1.0f;

    private HashSet<Vector3> firePositions = new HashSet<Vector3>();

    void Start()
    {
        // 1. 처음 불 찾기
        GameObject[] existingFires = GameObject.FindGameObjectsWithTag("Fire");
        
        if (existingFires.Length == 0)
        {
            Debug.LogError("❌ 처음 불을 못 찾았어요! Hierarchy에 있는 FireBall의 Tag가 'Fire'인지 확인하세요!");
            return; 
        }

        Debug.Log($"✅ 처음 불 {existingFires.Length}개 발견! 확산 시작합니다.");

        foreach (GameObject fire in existingFires)
        {
            Vector3 pos = SnapToGrid(fire.transform.position);
            if (!firePositions.Contains(pos))
            {
                firePositions.Add(pos);
            }
        }

        StartCoroutine(SpreadRoutine());
    }

    IEnumerator SpreadRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spreadTime);

            List<Vector3> currentFires = new List<Vector3>(firePositions);
            Debug.Log("🔥 불 확산 시도 중... 현재 불 개수: " + currentFires.Count);

            foreach (Vector3 pos in currentFires)
            {
                TrySpread(pos + Vector3.forward * gridSize); // 위
                TrySpread(pos + Vector3.back * gridSize);    // 아래
                TrySpread(pos + Vector3.left * gridSize);    // 왼쪽
                TrySpread(pos + Vector3.right * gridSize);   // 오른쪽
            }
        }
    }

    void TrySpread(Vector3 targetPos)
    {
        if (firePositions.Contains(targetPos)) return;

        // 수정된 부분: 바닥 말고 'Wall' 태그만 피하도록 변경
        Collider[] hitColliders = Physics.OverlapSphere(targetPos, 0.4f);
        foreach(var col in hitColliders)
        {
            if(col.CompareTag("Wall")) 
            {
                // 벽이 있으면 안 번짐 (조용히 리턴)
                return; 
            }
        }

        // 불 생성
        Instantiate(firePrefab, targetPos, Quaternion.identity);
        firePositions.Add(targetPos);
    }

    Vector3 SnapToGrid(Vector3 rawPos)
    {
        float x = Mathf.Round(rawPos.x / gridSize) * gridSize;
        float z = Mathf.Round(rawPos.z / gridSize) * gridSize;
        return new Vector3(x, 0, z);
    }
}