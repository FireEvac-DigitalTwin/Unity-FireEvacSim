using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬의 모든 FloorArrow를 관리하고, 불 위치에 따라 최적 경로를 BFS로 재계산합니다.
/// GuidanceManager가 씬에 있으면 BFS 대신 NavMesh 기반 경로를 사용하므로
/// 이 스크립트의 라우팅은 자동 비활성화됩니다.
///
/// 알고리즘:
///   출구 화살표(isExit=true)에서 역방향 BFS → 각 화살표의 nextArrow 설정
///   불이 퍼질 때마다(updateInterval) 재계산 → 화살표 색/방향 자동 갱신
/// </summary>
[ExecuteAlways]
public class FloorArrowRouter : MonoBehaviour
{
    public static FloorArrowRouter Instance { get; private set; }

    [Header("라우팅 설정")]
    [Tooltip("경로 재계산 주기 (초). 불 확산 속도보다 짧게 설정 권장")]
    public float updateInterval = 2f;

    [Tooltip("이 반경 안에 불이 있으면 화살표 차단 (m)")]
    public float fireBlockRadius = 2.5f;

    private FloorArrow[] allArrows;

    // ── 초기화 ────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Application.isPlaying) { Destroy(gameObject); return; }
    }

    void OnEnable()
    {
        Instance = this;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += EditorRefresh;
        }
#endif
    }

    void Start()
    {
        // GuidanceManager가 있으면 NavMesh 기반 경로를 사용하므로 BFS 비활성화
        if (Application.isPlaying && FindObjectOfType<GuidanceManager>() != null)
        {
            Debug.Log("[FloorArrowRouter] GuidanceManager 감지 → BFS 라우팅 비활성화 (NavMesh 경로 사용)");
            return;
        }

        RefreshArrowList();
        UpdateRouting();
        if (Application.isPlaying)
            InvokeRepeating(nameof(UpdateRouting), updateInterval, updateInterval);
    }

#if UNITY_EDITOR
    void EditorRefresh()
    {
        if (this == null) return;
        RefreshArrowList();
        UpdateRouting();
    }

    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += EditorRefresh;
    }
#endif

    /// <summary>씬에 새 화살표가 추가됐을 때 수동 호출 가능</summary>
    public void RefreshArrowList()
    {
        allArrows = FindObjectsOfType<FloorArrow>();
        Debug.Log($"[FloorArrowRouter] FloorArrow {allArrows.Length}개 등록.");
    }

    // ── 메인: 경로 재계산 ──────────────────────────────────────
    public void UpdateRouting()
    {
        if (allArrows == null || allArrows.Length == 0) return;

        // Step 1: 화재 근접 화살표 차단 여부 갱신
        foreach (var a in allArrows)
        {
            a.isBlocked = false;

            if (FireSpreadManager.Instance != null)
            {
                a.isBlocked = IsNearFire(a.transform.position);
            }
        }

        // Step 2: 전체 초기화
        foreach (var a in allArrows)
        {
            a.nextArrow      = null;
            a.routeDistance  = int.MaxValue;
        }

        // Step 3: 출구 화살표부터 BFS (역방향 = 출구 방향 부모 포인터 세팅)
        var queue = new Queue<FloorArrow>();

        foreach (var a in allArrows)
        {
            if (a.isExit && !a.isBlocked)
            {
                a.routeDistance = 0;
                queue.Enqueue(a);
            }
        }

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();

            foreach (var neighbor in cur.neighbors)
            {
                if (neighbor == null)          continue;
                if (neighbor.isBlocked)        continue;
                if (neighbor.routeDistance <= cur.routeDistance + 1) continue;

                neighbor.routeDistance = cur.routeDistance + 1;
                neighbor.nextArrow     = cur;
                queue.Enqueue(neighbor);
            }
        }

        // Step 4: 색상·방향 시각 갱신
        foreach (var a in allArrows)
            a.RefreshVisual();

        int reachable = CountReachable();
        Debug.Log($"[FloorArrowRouter] 경로 갱신 완료. 도달 가능 화살표: {reachable}/{allArrows.Length}");
    }

    // ── 헬퍼 ──────────────────────────────────────────────────
    bool IsNearFire(Vector3 pos)
    {
        if (FireSpreadManager.Instance == null) return false;

        float r = fireBlockRadius;
        Vector3[] checkPoints =
        {
            pos,
            pos + Vector3.forward * r,
            pos + Vector3.back    * r,
            pos + Vector3.right   * r,
            pos + Vector3.left    * r,
        };

        foreach (var pt in checkPoints)
            if (FireSpreadManager.Instance.IsNodeOnFire(pt))
                return true;

        return false;
    }

    int CountReachable()
    {
        int n = 0;
        foreach (var a in allArrows)
            if (a.nextArrow != null || a.isExit) n++;
        return n;
    }
}
