using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 복도 바닥에 배치되는 대피 유도 화살표.
///
/// [사용 방법]
///   1. 빈 GameObject에 이 컴포넌트를 추가
///   2. Inspector > Arrow Prefab 에 Untitled FBX(또는 Prefab)를 드래그
///      - 비워두면 코드로 생성한 폴리곤 화살표가 사용됩니다
///   3. 불 상황에 따라 색이 초록/빨강/회색으로 자동 변경됩니다
/// </summary>
[ExecuteAlways]
public class FloorArrow : MonoBehaviour
{
    [Header("인접 화살표 (Tools > Floor Arrows > Auto-Connect 또는 직접 연결)")]
    public List<FloorArrow> neighbors = new List<FloorArrow>();

    [Header("설정")]
    [Tooltip("실제 출구(문) 자체가 아니라, 출구 방향을 가리키는 '마지막 지시등(화살표)'에 체크")]
    public bool isExit = false;

    [Header("커스텀 화살표 모델 (선택 사항)")]
    [Tooltip("Untitled FBX 또는 Prefab을 여기에 드래그하세요.\n비워두면 코드 생성 폴리곤이 사용됩니다.")]
    public GameObject arrowPrefab;

    [Header("화살표 위치·크기 보정 (커스텀 모델용)")]
    public Vector3 prefabPositionOffset = Vector3.zero;
    public Vector3 prefabRotationOffset = Vector3.zero;
    public Vector3 prefabScale          = Vector3.one;

    [Header("폴리곤 화살표 크기 (커스텀 모델 없을 때)")]
    public float arrowLength = 1.0f;
    public float arrowWidth  = 0.6f;
    public float shaftWidth  = 0.22f;

    [Header("색상")]
    public Color activeColor   = new Color(0.1f, 0.92f, 0.25f);
    public Color blockedColor  = new Color(1f,   0.15f, 0.05f);
    public Color inactiveColor = new Color(0.4f, 0.4f,  0.4f);

    // FloorArrowRouter가 설정
    [HideInInspector] public FloorArrow nextArrow;
    [HideInInspector] public int        routeDistance = int.MaxValue;
    [HideInInspector] public bool       isBlocked     = false;

    private GameObject  arrowObj;
    private Renderer[]  arrowRenderers;

    // ── 생명주기 ──────────────────────────────────────────────
    void OnEnable()
    {
        // 에디터에서 도메인 리로드 중에는 비주얼 생성 건너뛰기 (freeze 방지)
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                EnsureVisual();
                RefreshVisual();
            };
            return;
        }
#endif
        EnsureVisual();
        RemoveColliders();
        RefreshVisual();
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            EnsureVisual();
            RefreshVisual();
        };
#endif
    }

    void OnDisable() => ClearVisual();
    void OnDestroy() => ClearVisual();

    /// <summary>화살표의 모든 Collider 제거 (불 확산 방해 방지)</summary>
    void RemoveColliders()
    {
        if (!Application.isPlaying) return;
        foreach (var col in GetComponentsInChildren<Collider>())
            Destroy(col);
    }

    // ── 비주얼 관리 ───────────────────────────────────────────
    void EnsureVisual()
    {
        if (arrowObj != null) return;

        // 이미 자식이 있으면 (Untitled 등 직접 배치한 모델) 그걸 사용
        if (transform.childCount > 0)
        {
            // ArrowVisual이 아닌 기존 자식(Untitled 등)을 우선 사용
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name != "ArrowVisual")
                {
                    arrowObj       = child.gameObject;
                    arrowRenderers = arrowObj.GetComponentsInChildren<Renderer>();

                    // 화살표 Collider 제거 (불 확산 방해 방지)
                    foreach (var col in arrowObj.GetComponentsInChildren<Collider>())
                    {
                        if (Application.isPlaying) Destroy(col);
                        else                       DestroyImmediate(col);
                    }
                    return;
                }
            }
            // ArrowVisual만 있는 경우
            arrowObj       = transform.GetChild(0).gameObject;
            arrowRenderers = arrowObj.GetComponentsInChildren<Renderer>();
            return;
        }

        BuildVisual();
    }

    void ClearVisual()
    {
        if (arrowObj == null) return;
        // ArrowVisual(코드 생성)만 삭제, Untitled 등 직접 배치한 모델은 보존
        if (arrowObj.name == "ArrowVisual")
        {
            if (Application.isPlaying) Destroy(arrowObj);
            else                       DestroyImmediate(arrowObj);
        }
        arrowObj       = null;
        arrowRenderers = null;
    }

    void BuildVisual()
    {
        if (arrowPrefab != null)
            BuildFromPrefab();
        else
            BuildPolygon();
    }

    // ── 커스텀 모델(Untitled FBX 등) 사용 ────────────────────
    void BuildFromPrefab()
    {
        arrowObj = Instantiate(arrowPrefab);
        arrowObj.name = "ArrowVisual";
        arrowObj.transform.SetParent(transform);
        arrowObj.transform.localPosition = prefabPositionOffset;
        arrowObj.transform.localRotation = Quaternion.Euler(prefabRotationOffset);
        arrowObj.transform.localScale    = prefabScale;

        // Collider 제거 (이동 방해 방지)
        foreach (var col in arrowObj.GetComponentsInChildren<Collider>())
        {
            if (Application.isPlaying) Destroy(col);
            else                       DestroyImmediate(col);
        }

        arrowRenderers = arrowObj.GetComponentsInChildren<Renderer>();

        // 각 파트에 색 변경 가능한 개별 Material 인스턴스 생성
        foreach (var r in arrowRenderers)
        {
            if (r.sharedMaterial != null)
                r.material = new Material(r.sharedMaterial);
        }
    }

    // ── 코드 생성 폴리곤 화살표 ──────────────────────────────
    void BuildPolygon()
    {
        arrowObj = new GameObject("ArrowVisual");
        arrowObj.transform.SetParent(transform);
        arrowObj.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        arrowObj.transform.localRotation = Quaternion.identity;
        arrowObj.transform.localScale    = Vector3.one;

        var mf   = arrowObj.AddComponent<MeshFilter>();
        var rend = arrowObj.AddComponent<MeshRenderer>();

        mf.sharedMesh = BuildArrowMesh();

        var mat = new Material(FindShader());
        mat.color = inactiveColor;
        mat.SetColor("_BaseColor", inactiveColor);
        rend.sharedMaterial = mat;

        arrowRenderers = new Renderer[] { rend };
    }

    Mesh BuildArrowMesh()
    {
        float L  = arrowLength;
        float HW = arrowWidth * 0.5f;
        float SW = shaftWidth * 0.5f;

        float tipZ    =  L * 0.5f;
        float headZ   =  L * 0.1f;
        float bottomZ = -L * 0.5f;

        var verts = new Vector3[]
        {
            new Vector3(  0,  0, tipZ),
            new Vector3(-HW,  0, headZ),
            new Vector3(-SW,  0, headZ),
            new Vector3(-SW,  0, bottomZ),
            new Vector3( SW,  0, bottomZ),
            new Vector3( SW,  0, headZ),
            new Vector3( HW,  0, headZ),
        };

        var tris = new int[]
        {
            0, 6, 1,
            1, 6, 5,
            1, 5, 2,
            2, 5, 4,
            2, 4, 3,
        };

        var normals = new Vector3[verts.Length];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

        var mesh = new Mesh { name = "FloorArrowMesh" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.normals   = normals;
        mesh.RecalculateBounds();
        return mesh;
    }

    // ── 색상·방향 갱신 ────────────────────────────────────────
    public void RefreshVisual()
    {
        if (arrowRenderers == null) return;

        Color c = isBlocked                     ? blockedColor  :
                  (nextArrow != null || isExit) ? activeColor   :
                                                  inactiveColor;

        foreach (var r in arrowRenderers)
        {
            if (r == null) continue;

            // 프리팹/런타임 모두 sharedMaterial만 사용해서 에디터 경고/에러 방지
            var mat = r.sharedMaterial;
            if (mat == null) continue;

            mat.color = c;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", c);
        }

        // GuidanceManager가 있으면 방향 제어를 GuidanceManager에 위임
        // GuidanceManager가 없을 때만 BFS(nextArrow) 기반으로 회전
        if (arrowObj != null && nextArrow != null && !isExit && GuidanceManager.Instance == null)
        {
            Vector3 dir = nextArrow.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                float worldY = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
                float localY = worldY - transform.eulerAngles.y;
                arrowObj.transform.localEulerAngles = new Vector3(90f, localY, 0f);
            }
        }
    }

    // ── Gizmo ─────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        Gizmos.color = isExit ? Color.yellow : new Color(0.2f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.2f);

        if (nextArrow != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.15f,
                            nextArrow.transform.position + Vector3.up * 0.15f);
        }

        foreach (var n in neighbors)
        {
            if (n == null) continue;
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
            Gizmos.DrawLine(transform.position, n.transform.position);
        }
    }

    static Shader FindShader() =>
        Shader.Find("Universal Render Pipeline/Lit")
     ?? Shader.Find("HDRP/Lit")
     ?? Shader.Find("Standard");
}
