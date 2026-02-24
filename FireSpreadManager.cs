using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FireSpreadManager : MonoBehaviour
{
    // --- 싱글톤 추가 시작 ---
    public static FireSpreadManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 씬 이동 시 파괴되지 않으려면 아래 주석 해제 (필요 시)
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    // --- 싱글톤 추가 끝 ---

    [Header("설정")]
    public GameObject firePrefab;
    public float spreadTime = 3.0f;
    public float gridSize = 1.0f;

    private HashSet<Vector3> firePositions = new HashSet<Vector3>();
    private List<Vector3> initialFirePositions = new List<Vector3>();
    private bool isSpreading = false;
    private Coroutine spreadCoroutine;

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
                initialFirePositions.Add(pos);
            }
        }

        StartSpread();
    }

    public void ResetFire()
    {
        // 현재 모든 Fire 오브젝트 제거
        GameObject[] fires = GameObject.FindGameObjectsWithTag("Fire");
        foreach (var f in fires)
        {
            // 인스펙터에 연결된 firePrefab(프리팹 또는 원본 오브젝트)은 삭제하지 않음
            if (firePrefab != null && f == firePrefab)
                continue;

            Destroy(f);
        }

        firePositions.Clear();

        // 처음 불 위치로 재생성
        foreach (var pos in initialFirePositions)
        {
            Instantiate(firePrefab, pos, Quaternion.identity);
            firePositions.Add(pos);
        }

        // 다시 확산 시작
        StartSpread();
    }

    IEnumerator SpreadRoutine()
    {
        while (isSpreading)
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

    public void StartSpread()
    {
        if (isSpreading) return;

        isSpreading = true;
        spreadCoroutine = StartCoroutine(SpreadRoutine());
        Debug.Log("🔥 FireSpreadManager: 에피소드 시작 - 불 확산 시작");
    }

    public void StopSpread()
    {
        if (!isSpreading) return;

        isSpreading = false;
        if (spreadCoroutine != null)
        {
            StopCoroutine(spreadCoroutine);
            spreadCoroutine = null;
        }

        Debug.Log("🔥 FireSpreadManager: 에피소드 종료 - 불 확산 정지");
    }

    void TrySpread(Vector3 targetPos)
    {
        if (firePositions.Contains(targetPos)) return;

        // 프리팹 참조가 사라진 경우 방어 코드
        if (firePrefab == null)
        {
            Debug.LogError("❌ FireSpreadManager: firePrefab이 비어 있거나 이미 삭제되었습니다. Project 창의 프리팹을 firePrefab에 다시 할당해주세요.");
            return;
        }

        // 벽('Wall' 태그)이 있는지 체크
        Collider[] hitColliders = Physics.OverlapSphere(targetPos, 0.4f);
        foreach(var col in hitColliders)
        {
            if(col.CompareTag("Wall")) 
            {
                return; 
            }
        }

        // 불 생성
        Instantiate(firePrefab, targetPos, Quaternion.identity);
        firePositions.Add(targetPos);
    }

    // --- 에이전트가 확인할 수 있는 함수 추가 ---
    /// <summary> 특정 위치에 불이 있는지 확인합니다. </summary>
    public bool IsNodeOnFire(Vector3 worldPosition)
    {
        Vector3 snappedPos = SnapToGrid(worldPosition);
        return firePositions.Contains(snappedPos);
    }

    Vector3 SnapToGrid(Vector3 rawPos)
    {
        float x = Mathf.Round(rawPos.x / gridSize) * gridSize;
        float z = Mathf.Round(rawPos.z / gridSize) * gridSize;
        // 에이전트의 Y축 위치와 상관없이 바닥 평면(Y=0) 기준으로 체크
        return new Vector3(x, 0, z);
    }
}