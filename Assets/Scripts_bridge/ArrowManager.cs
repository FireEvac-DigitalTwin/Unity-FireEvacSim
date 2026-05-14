using UnityEngine;
using System.Collections.Generic;

public class ArrowManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public GameObject arrowPrefab;
    public float cellSize = 10f;
    public int rows = 40;
    public int cols = 25;

    [Header("Arrow Colors")]
    public Color colorNorth = Color.blue;    // ↑ 방향 색상 (N)
    public Color colorSouth = Color.red;     // ↓ 방향 색상 (S)
    public Color colorEast = Color.green;    // → 방향 색상 (E)
    public Color colorWest = Color.yellow;   // ← 방향 색상 (W)

    public List<GameObject> spawnedArrows = new List<GameObject>();
    
    // 💡 최적화: 매번 GetComponent를 하지 않기 위해 Renderer를 미리 캐싱(저장)해 둡니다.
    private List<Renderer> arrowRenderers = new List<Renderer>();
    
    private Dictionary<(int, int), int> lightCellIndex = new Dictionary<(int, int), int>();

    private int[,] grid = new int[40, 25] {
    {3,3,3,3,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,1,1,1,1,1,1,1},
    {1,1,1,1,0,0,0,1,3,3,1,3,3,1,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,1,3,3,1,3,3,1,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,1,3,3,3,3,3,1,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,3,0,0,0,1,3,1,3,1,3,1,0,0,0,1,1,1,1,1,1,1,1},
    {1,1,1,1,0,0,0,1,1,1,3,1,1,1,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,3,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,2,2,2,2,1,1,1}, //Exit A (10,18~21)
    {1,1,1,1,0,0,0,1,1,1,1,1,1,1,0,0,0,0,0,0,0,0,1,1,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,0,0,0,0,0,1,1,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,0,0,0,0,0,1,1,1},
    {3,3,3,3,0,0,0,1,1,1,1,1,1,1,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,0,0,0,0,0,0,0,0,0,0,1,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,3,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,3,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,3,3,3,3,3,3,3,1},
    {3,3,3,3,0,0,0,1,1,1,1,1,1,1,0,0,0,1,3,3,3,3,3,3,1},
    {1,1,1,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,3,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,3,3,3,3,3,3,1},
    {1,1,1,1,0,0,0,1,1,1,1,1,1,1,0,0,0,3,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,3,3,3,3,3,3,1},
    {3,3,3,3,0,0,0,0,0,0,0,0,0,0,0,0,0,1,3,3,3,3,3,3,1},
    {1,1,1,1,0,0,0,0,2,2,0,0,0,0,0,0,0,1,3,3,3,3,3,3,1}, //Exit B (29,8~9 )
    {3,3,3,3,0,0,0,1,1,1,1,1,1,1,0,0,0,3,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,1,1,1,1,1,1,0,0,0,1,1,1,1,1,1,1,1},
    {3,3,3,1,0,0,0,0,0,0,0,0,0,0,0,0,0,3,3,3,3,3,3,3,1},
    {1,1,1,1,0,0,0,0,0,0,0,0,0,0,0,0,0,3,3,3,3,3,3,3,1},
    {3,3,3,3,0,0,0,1,1,1,3,1,1,1,0,0,0,1,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,3,1,3,1,3,1,0,0,0,1,3,3,3,3,3,3,1},
    {3,3,3,1,0,0,0,1,3,3,3,3,3,1,0,0,0,1,3,3,3,3,3,3,1},
    {1,1,1,1,0,0,0,1,3,3,1,3,3,1,0,0,0,1,3,3,3,3,3,3,1},
    {3,3,3,3,0,0,0,1,3,3,1,3,3,1,0,0,0,3,3,3,3,3,3,3,1},
    {3,3,3,3,0,0,0,1,3,3,1,3,3,1,0,0,0,3,3,3,3,3,3,3,1}
    };

    const int HALL = 0, WALL = 1, EXIT = 2, ROOM = 3;

    void Start() {
        SpawnArrows();
    }

    public void SpawnArrows() {
        foreach (var arrow in spawnedArrows) {
            if (arrow != null) Destroy(arrow);
        }
        spawnedArrows.Clear();
        arrowRenderers.Clear(); // 렌더러 리스트도 초기화
        lightCellIndex.Clear();

        Quaternion baseRot = transform.rotation * Quaternion.Euler(90f, 0f, 0f);

        int arrowCount = 0;
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < cols; c++) {
                int cellType = grid[r, c];
                
                bool isWalkable = (cellType == HALL || cellType == EXIT || cellType == ROOM);
                bool isExit = (cellType == EXIT);
                
                if (isWalkable && !isExit) {
                    Vector3 pos = GridToWorld(r, c);
                    
                    GameObject arrow = Instantiate(arrowPrefab, pos, baseRot);
                    arrow.transform.SetParent(this.transform);
                    arrow.name = $"Arrow_{r}_{c}";
                    
                    spawnedArrows.Add(arrow);

                    // 💡 화살표의 색상을 바꾸기 위해 MeshRenderer를 찾아서 리스트에 저장해 둡니다.
                    Renderer renderer = arrow.GetComponentInChildren<Renderer>();
                    arrowRenderers.Add(renderer);

                    lightCellIndex[(r, c)] = arrowCount;
                    arrowCount++;
                }
            }
        }
        Debug.Log($"✅ 총 {spawnedArrows.Count}개의 화살표가 생성되었습니다.");
    }

    public void UpdateArrowDirections(Dictionary<string, int> lightDirs) {
        if (lightDirs == null) {
            Debug.LogError("❌ light_dirs가 null입니다!");
            return;
        }

        int updatedCount = 0;
        foreach (var kvp in lightDirs) {
            string[] parts = kvp.Key.Split(',');
            if (parts.Length != 2) continue;

            int r = int.Parse(parts[0].Trim());
            int c = int.Parse(parts[1].Trim());
            int direction = kvp.Value;

            if (lightCellIndex.TryGetValue((r, c), out int arrowIndex)) {
                // 1. 회전 적용
                float rotationY = GetRotationAngle(direction);
                spawnedArrows[arrowIndex].transform.localRotation = Quaternion.Euler(90f, rotationY, 0f);

                // 2. 💡 색상 적용
                if (arrowRenderers[arrowIndex] != null) {
                    arrowRenderers[arrowIndex].material.color = GetDirectionColor(direction);
                }

                updatedCount++;
            }
        }
    }

    private float GetRotationAngle(int direction) {
        return direction switch {
            0 => 0f,      // N (↑)
            1 => 180f,    // S (↓)
            2 => 90f,     // E (→)
            3 => 270f,    // W (←)
            _ => 0f
        };
    }

    // 💡 방향에 따른 색상을 반환하는 함수
    private Color GetDirectionColor(int direction) {
        return direction switch {
            0 => colorNorth,
            1 => colorSouth,
            2 => colorEast,
            3 => colorWest,
            _ => Color.white
        };
    }

    public Vector3 GridToWorld(int r, int c) {
        return transform.position + (transform.right * c * cellSize) + (transform.forward * -r * cellSize);
    }

    public float[] GetChannel0Data() {
        float[] data = new float[rows * cols];
        int index = 0;
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < cols; c++) {
                data[index] = (float)grid[r, c];
                index++;
            }
        }
        return data;
    }
}