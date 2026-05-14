using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 파이썬 데이터를 읽어 전체 출구들의 차단(Blocked) 상태를 업데이트합니다.
/// </summary>
public class BlockedExitManager : MonoBehaviour
{
    [SerializeField] private PythonBridge pythonBridge;
    [SerializeField] private ExitDoorVisual[] allExitsInScene; // 씬에 있는 모든 출구를 연결

    // "row,col" 문자열을 키로 하여 출구를 빠르게 찾기 위한 딕셔너리
    private Dictionary<string, ExitDoorVisual> exitDict = new Dictionary<string, ExitDoorVisual>();

    void Start()
    {
        if (pythonBridge == null)
            pythonBridge = FindObjectOfType<PythonBridge>();

        // 배열에 등록된 출구들을 딕셔너리에 매핑
        foreach (var exit in allExitsInScene)
        {
            string key = $"{exit.row},{exit.col}";
            if (!exitDict.ContainsKey(key))
            {
                exitDict.Add(key, exit);
            }
            else
            {
                Debug.LogWarning($"[BlockedExitManager] 중복된 출구 좌표가 있습니다: {key}");
            }
        }

        // 브리지의 스냅샷 이벤트 구독
        if (pythonBridge != null)
            pythonBridge.SnapshotReceivedEvent += OnSnapshotReceived;
    }

    void OnSnapshotReceived(PythonBridge.StepSnapshot snapshot)
    {
        if (snapshot == null) return;

        // 1. 이번 스텝에서 막힌 출구 좌표들을 HashSet으로 정리 (검색을 빠르게)
        HashSet<string> currentBlockedCells = new HashSet<string>();
        if (snapshot.blocked_exits != null)
        {
            foreach (var cell in snapshot.blocked_exits)
            {
                currentBlockedCells.Add($"{cell[0]},{cell[1]}"); // "row,col" 형태
            }
        }

        // 2. 씬에 있는 모든 출구를 돌면서, 현재 막힌 목록에 있으면 닫고 아니면 엽니다.
        foreach (var kvp in exitDict)
        {
            string cellKey = kvp.Key;
            ExitDoorVisual exitVisual = kvp.Value;

            bool isBlockedNow = currentBlockedCells.Contains(cellKey);
            exitVisual.SetBlocked(isBlockedNow);
        }
    }

    void OnDestroy()
    {
        if (pythonBridge != null)
            pythonBridge.SnapshotReceivedEvent -= OnSnapshotReceived;
    }
}