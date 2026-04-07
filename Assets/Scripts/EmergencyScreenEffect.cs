using UnityEngine;

/// <summary>
/// 비상 상황 시 바닥 Quad 위에 빨간 투명 메시를 올려 심장박동처럼 깜빡입니다.
/// Claude/비상 조명 UI 세팅 메뉴로 자동 생성하거나, Inspector에서 floorQuad를 연결하세요.
/// FireSpreadManager.OnFireStarted / AgentMover.OnEvacuationStarted 이벤트에 자동 연결됩니다.
/// </summary>
public class EmergencyScreenEffect : MonoBehaviour
{
    [Header("바닥 Quad 기준 (비워두면 기본값 사용)")]
    public Transform floorQuad;

    [Header("깜빡임 설정")]
    public float speed    = 3f;     // 깜빡이는 속도 (높을수록 빠름)
    [Range(0f, 1f)]
    public float maxAlpha = 0.35f;  // 최대 투명도

    private MeshRenderer flashRenderer;
    private Material      flashMaterial;
    private bool          isActive = false;

    void Awake()
    {
        BuildFlashQuad();
        FireSpreadManager.OnFireStarted  += StartEmergency;
        AgentMover.OnEvacuationStarted   += StartEmergency;
    }

    void OnDestroy()
    {
        FireSpreadManager.OnFireStarted  -= StartEmergency;
        AgentMover.OnEvacuationStarted   -= StartEmergency;
    }

    void BuildFlashQuad()
    {
        // 바닥 Quad 기준값 (없으면 씬 실측값 사용)
        Vector3    pos   = new Vector3(-20f, 0f, -5f);
        Quaternion rot   = Quaternion.Euler(90f, 0f, 0f);
        Vector3    scale = new Vector3(200f, 110f, 1f);

        if (floorQuad != null)
        {
            pos   = floorQuad.position;
            rot   = floorQuad.rotation;
            scale = floorQuad.localScale;
        }

        pos.y += 0.02f; // z-fighting 방지

        var go = new GameObject("EmergencyFlashQuad");
        go.transform.SetParent(transform);
        go.transform.position   = pos;
        go.transform.rotation   = rot;
        go.transform.localScale = scale;

        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        flashRenderer = go.AddComponent<MeshRenderer>();
        flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flashRenderer.receiveShadows    = false;

        // URP Unlit 투명 Material
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Standard");

        flashMaterial = new Material(shader);
        flashMaterial.SetFloat("_Surface", 1f);
        flashMaterial.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        flashMaterial.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        flashMaterial.SetInt("_ZWrite",    0);
        flashMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        flashMaterial.renderQueue = 3000;
        flashMaterial.SetColor("_BaseColor", new Color(1f, 0f, 0f, 0f));

        flashRenderer.material = flashMaterial;
        go.SetActive(false);
    }

    void Update()
    {
        if (!isActive || flashMaterial == null) return;

        // Sin 함수로 0 ~ maxAlpha 심장박동 효과
        float alpha = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f * maxAlpha;
        flashMaterial.SetColor("_BaseColor", new Color(1f, 0f, 0f, alpha));
    }

    /// <summary>비상 상황 시작</summary>
    public void StartEmergency()
    {
        isActive = true;
        if (flashRenderer != null)
            flashRenderer.gameObject.SetActive(true);
    }

    /// <summary>비상 상황 종료</summary>
    public void StopEmergency()
    {
        isActive = false;
        if (flashRenderer != null)
        {
            flashMaterial.SetColor("_BaseColor", new Color(1f, 0f, 0f, 0f));
            flashRenderer.gameObject.SetActive(false);
        }
    }
}
