using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파이썬 서버의 light_dirs를 Unity의 ArrowManager와 동기화합니다.
/// </summary>
public class ArrowSynchronizer : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PythonBridge pythonBridge;
    [SerializeField] private ArrowManager arrowManager;

    private void Start()
    {
        if (pythonBridge == null)
            pythonBridge = GetComponent<PythonBridge>();

        if (arrowManager == null)
            arrowManager = FindObjectOfType<ArrowManager>();

        if (pythonBridge != null)
        {
            // 💡 핵심 수정: Action이 아니라 Snapshot 이벤트를 구독합니다.
            pythonBridge.SnapshotReceivedEvent += OnSnapshotReceived;
        }
    }

    /// <summary>
    /// 스냅샷 데이터에서 light_dirs를 추출하여 화살표 업데이트
    /// </summary>
    private void OnSnapshotReceived(PythonBridge.StepSnapshot snapshot)
    {
        // 💡 주머니(Snapshot)에서 light_dirs를 꺼냅니다.
        if (snapshot == null || snapshot.light_dirs == null)
            return;

        if (arrowManager != null)
        {
            arrowManager.UpdateArrowDirections(snapshot.light_dirs);
        }
    }

    private void OnDestroy()
    {
        if (pythonBridge != null)
            pythonBridge.SnapshotReceivedEvent -= OnSnapshotReceived;
    }
}