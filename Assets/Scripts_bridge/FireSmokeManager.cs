using UnityEngine;
using System.Collections.Generic;

public class FireSmokeManager : MonoBehaviour
{
    [Header("서버 연동")]
    public PythonBridge pythonBridge;

    [Header("파티클 프리팹")]
    public GameObject firePrefab;  // 여기에 Unity Particle Pack의 불 프리팹 할당
    public GameObject smokePrefab; // 여기에 Unity Particle Pack의 연기 프리팹 할당

    [Header("그리드 설정 (상상관 도면 기준)")]
    public float cellSize = 1.0f; // 1셀당 실제 Unity 공간에서의 크기 (예: 1m)
    public Vector3 gridOrigin = Vector3.zero; // 배열 (0,0)에 해당하는 Unity 월드 시작 좌표

    // 현재 켜져 있는 불과 연기를 좌표 문자열("row,col")로 추적하는 딕셔너리
    private Dictionary<string, GameObject> activeFires = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> activeSmokes = new Dictionary<string, GameObject>();

    void Start()
    {
        // PythonBridge의 스냅샷 이벤트에 함수 연결
        if (pythonBridge != null)
            pythonBridge.SnapshotReceivedEvent += UpdateEffects;
    }

    // 매 스텝마다 파이썬 서버로부터 스냅샷을 받을 때 실행됨
    void UpdateEffects(PythonBridge.StepSnapshot snapshot)
    {
        // 💡 이 로그가 콘솔에 뜨는지 확인하세요!
        int fireCount = (snapshot.fire_cells != null) ? snapshot.fire_cells.Count : 0;
        Debug.Log($"🔥 [FireSmokeManager] 서버에서 받은 불 좌표 개수: {fireCount}");
        
        // 1. 불 파티클 업데이트
        UpdateParticleGroup(snapshot.fire_cells, activeFires, firePrefab);

        // 2. 연기 파티클 업데이트
        UpdateParticleGroup(snapshot.smoke_cells, activeSmokes, smokePrefab);
    }

    void UpdateParticleGroup(List<int[]> serverCells, Dictionary<string, GameObject> activeDict, GameObject prefab)
    {
        // 서버에서 온 이번 스텝의 좌표들을 저장할 임시 Set
        HashSet<string> currentCells = new HashSet<string>();

        if (serverCells != null)
        {
            foreach (var cell in serverCells)
            {
                int row = cell[0];
                int col = cell[1];
                string key = $"{row},{col}";
                currentCells.Add(key);

                // 새롭게 불/연기가 번진 좌표라면 유니티 화면에 생성
                if (!activeDict.ContainsKey(key))
                {
                    Vector3 worldPos = GridToWorldPosition(row, col);
                    GameObject newEffect = Instantiate(prefab, worldPos, Quaternion.identity, this.transform);
                    activeDict.Add(key, newEffect);
                }
            }
        }

        // 불이 꺼지거나 연기가 걷힌 곳(서버 데이터엔 없는데 유니티엔 켜져 있는 곳) 삭제
        List<string> keysToRemove = new List<string>();
        foreach (var key in activeDict.Keys)
        {
            if (!currentCells.Contains(key))
            {
                Destroy(activeDict[key]);
                keysToRemove.Add(key);
            }
        }

        // 딕셔너리에서 목록 정리
        foreach (var key in keysToRemove)
        {
            activeDict.Remove(key);
        }
    }

    // 2D 배열 좌표(row, col)를 상상관 3D 월드 좌표(X, Y, Z)로 변환
    Vector3 GridToWorldPosition(int row, int col)
    {
        // 2D 배열의 row는 아래로 갈수록 커지므로 보통 Unity의 -Z축과 매핑합니다.
        // col은 오른쪽으로 가므로 Unity의 +X축과 매핑합니다.
        float x = gridOrigin.x + (col * cellSize);
        float z = gridOrigin.z - (row * cellSize); 
        
        return new Vector3(x, gridOrigin.y, z);
    }

    void OnDestroy()
    {
        // 스크립트가 파괴될 때 이벤트 연결 해제 (메모리 누수 방지)
        if (pythonBridge != null)
            pythonBridge.SnapshotReceivedEvent -= UpdateEffects;
    }
}