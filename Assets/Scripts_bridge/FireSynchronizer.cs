using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파이썬 서버의 불 위치를 Unity 씬과 동기화합니다.
/// </summary>
public class FireSynchronizer : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private float cellSize = 5f;
    [SerializeField] private float fireHeight = 0.5f;

    [Header("참조")]
    [SerializeField] private PythonBridge pythonBridge;
    [SerializeField] private Transform gridOrigin;
    [SerializeField] private GameObject firePrefab;

    private Dictionary<Vector2Int, GameObject> fireGameObjects = new Dictionary<Vector2Int, GameObject>();
    private HashSet<Vector2Int> currentFirePositions = new HashSet<Vector2Int>();

    private void Start()
    {
        if (pythonBridge == null)
            pythonBridge = GetComponent<PythonBridge>();

        if (gridOrigin == null)
        {
            Debug.LogError("❌ gridOrigin이 설정되지 않았습니다!");
            gridOrigin = transform;
        }

        if (pythonBridge != null)
        {
            pythonBridge.SnapshotReceivedEvent += OnSnapshotReceived;
        }
    }

    private void OnSnapshotReceived(PythonBridge.StepSnapshot snapshot)
    {
        if (snapshot == null || snapshot.fire_cells == null)
            return;

        currentFirePositions.Clear();

        // 현재 불의 위치들 처리
        foreach (var fireCell in snapshot.fire_cells)
        {
            if (fireCell == null || fireCell.Length < 2) continue;

            int row = fireCell[0];
            int col = fireCell[1];
            Vector2Int key = new Vector2Int(col, row);

            currentFirePositions.Add(key);

            // GameObject가 없으면 해당 위치에 생성
            if (!fireGameObjects.ContainsKey(key))
            {
                CreateFireGameObject(row, col);
            }
        }

        // 제거된(진화된) 불 처리
        var removedFires = new List<Vector2Int>();
        foreach (var firePos in fireGameObjects.Keys)
        {
            if (!currentFirePositions.Contains(firePos))
                removedFires.Add(firePos);
        }

        foreach (var firePos in removedFires)
        {
            if (fireGameObjects.TryGetValue(firePos, out var fireGO))
            {
                Destroy(fireGO);
                fireGameObjects.Remove(firePos);
            }
        }

        // 디버그 로그 (기존과 동일)
        // Debug.Log($"🔥 불 위치 업데이트: {currentFirePositions.Count}개");
    }

    private Vector3 GridToWorldPosition(int row, int col)
    {
        // 💡 핵심 수정: 에이전트와 동일하게 파이썬의 row가 Z축 역방향(-Z)으로 뻗어 나가도록 수정
        Vector3 worldPos = gridOrigin.position 
            + gridOrigin.right * col * cellSize 
            + gridOrigin.forward * -row * cellSize; // 👈 마이너스(-)를 추가했습니다.
        
        worldPos.y = gridOrigin.position.y + fireHeight;
        return worldPos;
    }

    private void CreateFireGameObject(int row, int col)
    {
        GameObject fireGO;

        if (firePrefab != null)
        {
            fireGO = Instantiate(firePrefab, GridToWorldPosition(row, col), Quaternion.identity);
        }
        else
        {
            // 파티클 프리팹이 없을 경우 기본 시각화 (주황색 큐브)
            fireGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fireGO.transform.localScale = Vector3.one * 0.9f;

            // 시각화 전용이므로 충돌체 제거
            Collider collider = fireGO.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = fireGO.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = new Color(1f, 0.3f, 0f);  // 주황색
            }

            fireGO.transform.position = GridToWorldPosition(row, col);
        }

        fireGO.name = $"Fire_{row}_{col}";
        Vector2Int key = new Vector2Int(col, row);
        fireGameObjects[key] = fireGO;
    }

    public int GetFireCount() => fireGameObjects.Count;

    private void OnDestroy()
    {
        if (pythonBridge != null)
            pythonBridge.SnapshotReceivedEvent -= OnSnapshotReceived;
    }
}