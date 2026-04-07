using UnityEngine;

public class TimeManager : MonoBehaviour
{
    void Start()
    {
        // ⭐ 게임 시작 시 콘솔창에서 설정을 확인하세요!
        Debug.Log("🚀 [배속 시스템] 준비 완료! F1: 1배속 / F2: 2배속 / F3: 3배속");
    }

    void Update()
    {
        // F1 키: 1배속 (정상 속도)
        if (Input.GetKeyDown(KeyCode.F1)) ChangeSpeed(1f);
        
        // F2 키: 2배속
        if (Input.GetKeyDown(KeyCode.F2)) ChangeSpeed(2f);
        
        // F3 키: 3배속 
        if (Input.GetKeyDown(KeyCode.F3)) ChangeSpeed(3f);
    }

    void ChangeSpeed(float speed)
    {
        Time.timeScale = speed;
        Debug.Log($"⏩ 현재 속도: {speed}배속으로 변경되었습니다.");
    }

    private void OnDisable()
    {
        // 시뮬레이션이 멈추면 시간을 다시 정상(1배속)으로 돌려놓습니다.
        Time.timeScale = 1f;
    }
}