using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic; // List 관리를 위해 추가

public class EvacuationAgent : MonoBehaviour
{
    [Header("설정")]
    public Transform exitPoint;
    public float startDelay = 15.0f;
    
    // 💡 (새로 추가) 인스펙터에 나타나야 할 변수들!
    [Header("경로 시각화 설정")] 
    public GameObject arrowPrefab;
    public float arrowSpacing = 1.0f; // 화살표 간 간격
    public float arrowHeight = 0.1f;  // 바닥으로부터의 높이

    private NavMeshAgent agent;
    // 💡 (새로 추가) 현재 생성된 화살표들을 관리할 리스트
    private List<GameObject> activeArrows = new List<GameObject>(); 

    IEnumerator Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // 1. 처음엔 제자리에 멈춰 있음 (속도 0)
        agent.speed = 0; 

        // 2. 설정한 시간만큼 기다림
        yield return new WaitForSeconds(startDelay);

        // 3. 시간이 지나면 달리기 시작!
        agent.speed = 8;
        
        if (exitPoint != null)
        {
            agent.SetDestination(exitPoint.position);
            
            // 💡 경로 표시 코루틴 시작
            StartCoroutine(DrawPathRoutine());
        }
    }

    // 💡 경로를 계속 업데이트하며 그리는 코루틴
    IEnumerator DrawPathRoutine()
    {
        while (true)
        {
            // 0.5초마다 업데이트
            yield return new WaitForSeconds(0.5f); 
            
            if (agent.hasPath)
            {
                GenerateArrowPath(agent.path);
            }
            else
            {
                ClearArrows(); // 경로가 없다면 화살표를 모두 지움
            }
        }
    }

    // 💡 NavMeshPath를 기반으로 3D 화살표를 생성 및 배치하는 함수
    void GenerateArrowPath(NavMeshPath path)
    {
        if (arrowPrefab == null) return; // 프리팹이 연결 안 됐으면 실행 안 함
        
        ClearArrows(); // 이전 화살표 모두 제거

        Vector3[] corners = path.corners;
        if (corners.Length < 2) return;

        // 경로의 각 코너(꺾이는 지점) 사이를 순회하며 화살표 배치
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 start = corners[i];
            Vector3 end = corners[i + 1];
            
            float segmentLength = Vector3.Distance(start, end);
            int numArrows = Mathf.FloorToInt(segmentLength / arrowSpacing);
            
            // 화살표가 바라볼 방향 설정
            Quaternion rotation = Quaternion.LookRotation(end - start); 

            for (int j = 0; j < numArrows; j++)
            {
                float t = (j * arrowSpacing) / segmentLength;
                Vector3 position = Vector3.Lerp(start, end, t);
                
                position.y += arrowHeight; // 바닥 높이 조정

                // 💡 화살표 프리팹 생성 및 방향 설정
                GameObject arrow = Instantiate(arrowPrefab, position, rotation);
                activeArrows.Add(arrow);
                arrow.transform.SetParent(transform); // 에이전트의 자식으로 설정
            }
        }
    }

    // 💡 생성된 모든 화살표를 파괴하는 함수
    void ClearArrows()
    {
        foreach (GameObject arrow in activeArrows)
        {
            if (arrow != null)
            {
                Destroy(arrow);
            }
        }
        activeArrows.Clear();
    }
}