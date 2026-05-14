using UnityEngine;
using System.Collections;

/// <summary>
/// 개별 출구의 방화 셔터, X마크, 경광등을 제어합니다.
/// 셔터가 천장에서부터 롤스크린처럼 내려오도록 Scale과 Position을 동시에 제어합니다.
/// </summary>
public class ExitDoorVisual : MonoBehaviour
{
    [Header("그리드 좌표 매핑")]
    [Tooltip("파이썬 환경 상의 행(row) 좌표")]
    public int row;
    [Tooltip("파이썬 환경 상의 열(col) 좌표")]
    public int col;

    [Header("시각적 요소 참조")]
    public Transform fireShutter;   // 내려올 방화 셔터 모델링
    public GameObject redXMark;     // 막혔을 때 띄울 빨간색 ❌ 아이콘
    public GameObject warningLight; // 막혔을 때 켤 붉은색 Point Light 또는 경광등

    [Header("셔터 세팅")]
    public float shutterSpeed = 5.0f; // 셔터가 늘어나는 속도

    private bool isBlocked = false;

    // 셔터의 스케일과 위치를 저장할 변수
    private Vector3 closedScale;
    private Vector3 closedPos;
    private Vector3 openScale;
    private Vector3 openPos;

    void Awake() 
    {
        // 💡 중요: 유니티 에디터에 세팅된 현재 모습을 "완전히 닫힌 상태"로 기억합니다.
        if (fireShutter != null)
        {
            closedScale = fireShutter.localScale;
            closedPos = fireShutter.localPosition;

            // "열린 상태" 계산: Y축 스케일을 0으로 만들고, 위치를 크기의 절반만큼 위로 올려 천장에 붙입니다.
            openScale = new Vector3(closedScale.x, 0f, closedScale.z);
            openPos = closedPos + new Vector3(0f, closedScale.y / 2f, 0f);
        }
    }

    void Start()
    {
        // 게임이 시작되면 일단 셔터를 위로 돌돌 말아 올립니다(열림 상태).
        ForceSetVisuals(false);
    }

    public void SetBlocked(bool blocked)
    {
        if (isBlocked == blocked) return; 
        isBlocked = blocked;

        if (redXMark != null) redXMark.SetActive(isBlocked);
        if (warningLight != null) warningLight.SetActive(isBlocked);

        if (fireShutter != null)
        {
            StopAllCoroutines();
            StartCoroutine(AnimateShutter(isBlocked));
        }
    }

    private void ForceSetVisuals(bool blocked)
    {
        isBlocked = blocked;
        if (redXMark != null) redXMark.SetActive(blocked);
        if (warningLight != null) warningLight.SetActive(blocked);
        
        if (fireShutter != null) 
        {
            fireShutter.localScale = blocked ? closedScale : openScale;
            fireShutter.localPosition = blocked ? closedPos : openPos;
        }
    }

    // 셔터를 부드럽게 늘리거나 줄이는 코루틴
    IEnumerator AnimateShutter(bool closeDoor)
    {
        Vector3 targetScale = closeDoor ? closedScale : openScale;
        Vector3 targetPos = closeDoor ? closedPos : openPos;

        // 스케일과 위치를 동시에 Lerp로 변경
        while (Vector3.Distance(fireShutter.localScale, targetScale) > 0.001f)
        {
            fireShutter.localScale = Vector3.Lerp(fireShutter.localScale, targetScale, Time.deltaTime * shutterSpeed);
            fireShutter.localPosition = Vector3.Lerp(fireShutter.localPosition, targetPos, Time.deltaTime * shutterSpeed);
            yield return null;
        }
        
        // 최종 오차 보정
        fireShutter.localScale = targetScale; 
        fireShutter.localPosition = targetPos;
    }
}