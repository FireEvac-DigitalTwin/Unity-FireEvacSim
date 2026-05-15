using UnityEngine;
using System.Collections;

/// <summary>
/// 대피 시작 시 바닥 Quad 위에 빨간 투명 메시를 올려 깜빡이게 합니다.
/// EmergencyFlash 오브젝트에 이 컴포넌트를 추가하고,
/// 인스펙터에서 Floor Quad 필드에 씬의 Quad 오브젝트를 연결하세요.
/// </summary>
public class EmergencyFlashEffect : MonoBehaviour
{
    [Header("깜빡임 설정")]
    public Color flashColor    = new Color(1f, 0f, 0f, 0.4f);
    public float flashDuration = 0.3f;   // 한 번 깜빡이는 시간 (초)
    public float flashInterval = 0.5f;   // 깜빡임 간격 (초)
    public int   flashCount    = 8;      // 깜빡임 횟수 (0 = 무한)

    [Header("바닥 Quad 기준")]
    [Tooltip("씬의 바닥 Quad 오브젝트를 여기에 연결하세요")]
    public Transform floorQuad;

    private MeshRenderer flashRenderer;
    private Material     flashMaterial;

    void Awake()
    {
        BuildFloorOverlay();
        AgentMover.OnEvacuationStarted += StartFlashing;
    }

    void OnDestroy()
    {
        AgentMover.OnEvacuationStarted -= StartFlashing;
    }

    void BuildFloorOverlay()
    {
        // 바닥 Quad 기준으로 위치·회전·크기 결정 (기본값 = 씬의 Quad 실측)
        Vector3    pos   = new Vector3(-20f, 0f, -5f);
        Quaternion rot   = Quaternion.Euler(90f, 0f, 0f);
        Vector3    scale = new Vector3(200f, 110f, 1f);

        if (floorQuad != null)
        {
            pos   = floorQuad.position;
            rot   = floorQuad.rotation;
            scale = floorQuad.localScale;
        }

        // 바닥보다 살짝 위 (z-fighting 방지)
        pos.y += 0.02f;

        var flashGO = new GameObject("FlashQuad");
        flashGO.transform.SetParent(transform);
        flashGO.transform.position   = pos;
        flashGO.transform.rotation   = rot;
        flashGO.transform.localScale = scale;

        var mf = flashGO.AddComponent<MeshFilter>();
        mf.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        flashRenderer = flashGO.AddComponent<MeshRenderer>();
        flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flashRenderer.receiveShadows    = false;

        // URP Unlit 투명 Material 생성
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        flashMaterial = new Material(shader);
        flashMaterial.SetFloat("_Surface", 1f);   // 0=Opaque, 1=Transparent
        flashMaterial.SetFloat("_Blend",   0f);   // Alpha blend
        flashMaterial.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        flashMaterial.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        flashMaterial.SetInt("_ZWrite",    0);
        flashMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        flashMaterial.renderQueue = 3000;
        flashMaterial.SetColor("_BaseColor", new Color(flashColor.r, flashColor.g, flashColor.b, 0f));

        flashRenderer.material = flashMaterial;

        flashGO.SetActive(false);
    }

    public void StartFlashing()
    {
        flashRenderer.gameObject.SetActive(true);
        StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        int count = 0;

        while (flashCount == 0 || count < flashCount)
        {
            yield return Fade(0f, flashColor.a, flashDuration * 0.5f);
            yield return Fade(flashColor.a, 0f, flashDuration * 0.5f);

            count++;
            if (flashCount == 0 || count < flashCount)
                yield return new WaitForSeconds(flashInterval - flashDuration);
        }

        flashRenderer.gameObject.SetActive(false);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.001f);
            float alpha = Mathf.Lerp(from, to, t);
            flashMaterial.SetColor("_BaseColor", new Color(flashColor.r, flashColor.g, flashColor.b, alpha));
            yield return null;
        }
    }
}
