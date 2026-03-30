using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FireSpreadManager : MonoBehaviour
{
    // --- 싱글톤 추가 시작 ---
    public static FireSpreadManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 씬 이동 시 파괴되지 않으려면 아래 주석 해제 (필요 시)
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    // --- 싱글톤 추가 끝 ---

    [Header("설정")]
    public GameObject firePrefab;
    public float gridSize = 1.0f;

    [Header("초기 불 위치 (랜덤 설정)")]
    [Tooltip("true 이면 아래 영역 안에서 초기 불 1개의 위치를 랜덤으로 잡습니다.")]
    public bool useRandomInitialFire = true;
    [Tooltip("랜덤 초기 불 위치 영역의 중심 (씬에서 위치 지정)")]
    public Vector3 randomAreaCenter = Vector3.zero;
    [Tooltip("랜덤 초기 불 위치 영역의 크기 (X,Z 사용)")]
    public Vector3 randomAreaSize = new Vector3(10f, 0f, 10f);

    [Header("생성 보정/디버그")]
    [Tooltip("불/그리드가 존재하는 바닥 Y 높이 (기본 0). 바닥이 305 같은 높이면 여기를 305로 설정하세요.")]
    public float gridPlaneY = 0f;

    [Tooltip("불을 바닥에서 살짝 띄워 생성 (바닥에 묻혀 안 보이는 문제 방지)")]
    public float fireSpawnYOffset = 0.1f;

    [Tooltip("true면 확산 루프 시작 직후 1회 즉시 확산을 수행합니다.")]
    public bool spreadOnceImmediately = true;

    // ⭐ 논문 데이터 기반 가속 변수 추가
    [Header("화재 확산 가속 설정 (t-squared)")]
    public float initialSpreadTime = 25.0f;    // 초기 확산 속도 (20~30초 권장)
    public float minimumSpreadTime = 5.0f;     // 최고 확산 속도 (5초 미만으로 떨어지지 않음)
    [Range(0.1f, 1.0f)]
    public float accelerationFactor = 0.85f;   // 가속도 (0.85 = 15%씩 대기 시간 단축)

    // 불이 처음 시작될 때 한 번 발동하는 이벤트 (EmergencyScreenEffect 등이 구독)
    public static event System.Action OnFireStarted;

    public Vector3 LastFirePosition { get; private set; }

    private HashSet<Vector3> firePositions = new HashSet<Vector3>();
    private List<Vector3> initialFirePositions = new List<Vector3>(); // 에피소드 시작 불 위치
    private List<Vector3> fixedStartPositions = new List<Vector3>();  // 고정 모드 원본 위치
    private bool isSpreading = false;
    private Coroutine spreadCoroutine;

    void Start()
    {
        firePositions.Clear();
        initialFirePositions.Clear();

        if (useRandomInitialFire)
        {
            if (firePrefab == null)
            {
                Debug.LogError("❌ FireSpreadManager: firePrefab이 비어 있습니다. 초기 불을 랜덤으로 생성하려면 firePrefab을 할당하세요.");
                return;
            }

            if (!firePrefab.CompareTag("Fire"))
                Debug.LogWarning($"[FireSpreadManager] firePrefab의 Tag가 'Fire'가 아닙니다. 현재 Tag='{firePrefab.tag}'. FindGameObjectsWithTag/리셋 로직과 맞추려면 'Fire'로 설정하세요.");

            // 씬에 예전에 배치해 둔 FireBall 같은 불 오브젝트가 있으면 먼저 모두 정리
            ClearAllFireObjectsInScene();

            // 랜덤 영역 중심의 Y를 그리드 평면으로 사용 (바닥이 0이 아닌 씬 대응)
            gridPlaneY = randomAreaCenter.y;

            // 랜덤 영역 안에서 유효한 위치를 찾을 때까지 재시도
            int maxAttempts = 100;
            Vector3 spawnPos = Vector3.zero;
            bool found = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-randomAreaSize.x * 0.5f, randomAreaSize.x * 0.5f),
                    0f,
                    Random.Range(-randomAreaSize.z * 0.5f, randomAreaSize.z * 0.5f)
                );

                Vector3 candidate = SnapToGrid(randomAreaCenter + randomOffset);

                // NavMesh 위인지 확인
                UnityEngine.AI.NavMeshHit hit;
                if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out hit, gridSize, UnityEngine.AI.NavMesh.AllAreas))
                    continue;

                // 제외 구역 체크 (x:10~80, z:-60~-40)
                if (candidate.x >= 10f && candidate.x <= 80f &&
                    candidate.z >= -60f && candidate.z <= -40f)
                    continue;

                // 벽 겹침 체크 (격자 셀 전체가 벽 밖에 있는지)
                bool nearWall = false;
                foreach (var col in Physics.OverlapSphere(candidate, gridSize * 0.5f))
                {
                    if (col.CompareTag("Wall")) { nearWall = true; break; }
                }
                if (nearWall) continue;

                spawnPos = candidate;
                found = true;
                break;
            }

            if (!found)
            {
                Debug.LogError("❌ FireSpreadManager: 유효한 초기 불 위치를 찾지 못했습니다. randomAreaCenter/randomAreaSize를 확인하세요.");
                return;
            }

            SpawnFire(spawnPos);
            LastFirePosition = spawnPos;
            Debug.Log($"✅ 랜덤 초기 불 생성 위치: {spawnPos}");
        }
        else
        {
            // 기존 방식: 씬에 배치된 Fire 태그 오브젝트를 초기 불로 사용
            GameObject[] existingFires = GameObject.FindGameObjectsWithTag("Fire");

            if (existingFires.Length == 0)
            {
                Debug.LogError("❌ 처음 불을 못 찾았어요! Hierarchy에 있는 FireBall의 Tag가 'Fire'인지 확인하세요!");
                return;
            }

            Debug.Log($"✅ 처음 불 {existingFires.Length}개 발견! 확산 시작합니다.");

            foreach (GameObject fire in existingFires)
            {
                Vector3 pos = SnapToGrid(fire.transform.position);
                if (!firePositions.Contains(pos))
                {
                    firePositions.Add(pos);
                    initialFirePositions.Add(pos);
                    fixedStartPositions.Add(pos);
                }
            }

            // 씬 배치 불의 높이를 그리드 평면으로 사용
            if (existingFires.Length > 0)
                gridPlaneY = existingFires[0].transform.position.y;
        }

        StartSpread();
    }

    public void ResetFire()
    {
        StopSpread();
        ClearAllFireObjectsInScene();

        firePositions.Clear();
        initialFirePositions.Clear();

        if (useRandomInitialFire)
        {
            // 새 랜덤 위치 뽑기
            int maxAttempts = 100;
            Vector3 spawnPos = Vector3.zero;
            bool found = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-randomAreaSize.x * 0.5f, randomAreaSize.x * 0.5f),
                    0f,
                    Random.Range(-randomAreaSize.z * 0.5f, randomAreaSize.z * 0.5f)
                );

                Vector3 candidate = SnapToGrid(randomAreaCenter + randomOffset);

                UnityEngine.AI.NavMeshHit hit;
                if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out hit, gridSize, UnityEngine.AI.NavMesh.AllAreas))
                    continue;

                // 제외 구역 체크 (x:10~80, z:-60~-40)
                if (candidate.x >= 10f && candidate.x <= 80f &&
                    candidate.z >= -60f && candidate.z <= -40f)
                    continue;

                // 벽 겹침 체크 (격자 셀 전체가 벽 밖에 있는지)
                bool nearWall = false;
                foreach (var col in Physics.OverlapSphere(candidate, gridSize * 0.5f))
                {
                    if (col.CompareTag("Wall")) { nearWall = true; break; }
                }
                if (nearWall) continue;

                spawnPos = candidate;
                found = true;
                break;
            }

            if (!found)
            {
                Debug.LogError("❌ ResetFire: 유효한 초기 불 위치를 찾지 못했습니다.");
                return;
            }

            SpawnFire(spawnPos);
            LastFirePosition = spawnPos;
            initialFirePositions.Add(spawnPos);
            Debug.Log($"✅ 랜덤 초기 불 리셋 위치: {spawnPos}");
        }
        else
        {
            // 고정 위치 재사용
            foreach (var pos in fixedStartPositions)
            {
                Instantiate(firePrefab, pos, Quaternion.identity);
                firePositions.Add(pos);
                initialFirePositions.Add(pos);
            }
        }

        StartSpread();
    }

    void ClearAllFireObjectsInScene()
    {
        GameObject[] fires = GameObject.FindGameObjectsWithTag("Fire");
        foreach (var f in fires)
        {
            // 인스펙터에 연결된 firePrefab(씬에 상주시키고 싶은 원본)이면 남겨두고,
            // 랜덤 모드에서는 firePrefab도 일반적으로 프리팹 자산이므로 대부분 여기엔 안 걸립니다.
            if (firePrefab != null && f == firePrefab)
                continue;

            Destroy(f);
        }
    }

    IEnumerator SpreadRoutine()
    {
        // ⭐ 시작할 때 확산 주기를 초기값(25초)으로 설정
        float currentSpreadTime = initialSpreadTime;

        if (spreadOnceImmediately)
        {
            // 시작 직후 1회 확산(즉시 가시성/동작 확인용)
            SpreadOneStep();
        }

        while (isSpreading)
        {
            // ⭐ 가속된 시간만큼 대기
            yield return new WaitForSeconds(currentSpreadTime);
            SpreadOneStep(currentSpreadTime);

            // ⭐ 확산 후 다음 대기 시간을 단축 (가속)
            currentSpreadTime = currentSpreadTime * accelerationFactor;
            
            // ⭐ 최고 속도 제한 (아무리 빨라도 minimumSpreadTime 밑으로는 안 내려감)
            currentSpreadTime = Mathf.Max(currentSpreadTime, minimumSpreadTime);
        }
    }

    void SpreadOneStep(float currentSpreadTimeForLog = -1f)
    {
        List<Vector3> currentFires = new List<Vector3>(firePositions);
        if (currentSpreadTimeForLog >= 0f)
            Debug.Log($"🔥 불 확산 시도 중... 현재 불 개수: {currentFires.Count} / 다음 확산까지: {currentSpreadTimeForLog:F2}초");
        else
            Debug.Log($"🔥 불 즉시 확산(1회) ... 현재 불 개수: {currentFires.Count}");

        // 4방향 중 랜덤하게 1~4개 방향으로 확산 (불균일 확산)
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (Vector3 pos in currentFires)
        {
            // 방향 셔플
            for (int i = directions.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = directions[i]; directions[i] = directions[j]; directions[j] = tmp;
            }

            // 랜덤 개수(1~4)만큼만 확산 시도
            int spreadCount = Random.Range(1, directions.Length + 1);
            for (int i = 0; i < spreadCount; i++)
            {
                TrySpread(pos + directions[i] * gridSize);
            }
        }
    }

    public void StartSpread()
    {
        if (isSpreading) return;

        isSpreading = true;
        spreadCoroutine = StartCoroutine(SpreadRoutine());
        OnFireStarted?.Invoke();
        Debug.Log("🔥 FireSpreadManager: 에피소드 시작 - 불 확산 시작");
    }

    public void StopSpread()
    {
        if (!isSpreading) return;

        isSpreading = false;
        if (spreadCoroutine != null)
        {
            StopCoroutine(spreadCoroutine);
            spreadCoroutine = null;
        }

        Debug.Log("🔥 FireSpreadManager: 에피소드 종료 - 불 확산 정지");
    }

    void TrySpread(Vector3 targetPos)
    {
        if (firePositions.Contains(targetPos)) return;

        // 프리팹 참조가 사라진 경우 방어 코드
        if (firePrefab == null)
        {
            Debug.LogError("❌ FireSpreadManager: firePrefab이 비어 있거나 이미 삭제되었습니다. Project 창의 프리팹을 firePrefab에 다시 할당해주세요.");
            return;
        }

        // 벽('Wall' 태그)이 있는지 체크
        Collider[] hitColliders = Physics.OverlapSphere(targetPos, 0.4f);
        foreach(var col in hitColliders)
        {
            if(col.CompareTag("Wall")) 
            {
                return; 
            }
        }

        SpawnFire(targetPos);
    }

    void SpawnFire(Vector3 snappedPos)
    {
        if (firePrefab == null)
        {
            Debug.LogError("❌ FireSpreadManager: firePrefab이 비어 있습니다.");
            return;
        }

        Vector3 spawnPos = new Vector3(snappedPos.x, gridPlaneY + fireSpawnYOffset, snappedPos.z);
        Instantiate(firePrefab, spawnPos, Quaternion.identity);
        firePositions.Add(snappedPos);
    }

    // --- 에이전트가 확인할 수 있는 함수 추가 ---
    /// <summary> 특정 위치에 불이 있는지 확인합니다. </summary>
    public bool IsNodeOnFire(Vector3 worldPosition)
    {
        Vector3 snappedPos = SnapToGrid(worldPosition);
        return firePositions.Contains(snappedPos);
    }

    Vector3 SnapToGrid(Vector3 rawPos)
    {
        float x = Mathf.Round(rawPos.x / gridSize) * gridSize;
        float z = Mathf.Round(rawPos.z / gridSize) * gridSize;
        // Y는 별도 평면값(gridPlaneY)로 관리 (바닥이 0이 아닌 씬 대응)
        return new Vector3(x, gridPlaneY, z);
    }
}