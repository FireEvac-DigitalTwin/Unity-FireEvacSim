using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;

/// <summary>
/// NavMesh.CalculatePath를 사용해 각 바닥 화살표의 방향을 실시간으로 출구를 향하도록 제어합니다.
/// Scene(에디터) 모드와 Play 모드 모두에서 동작합니다.
///
/// [출구 인식 방법] (둘 다 사용 가능)
///   - FloorArrow의 isExit = true
///   - 오브젝트에 "Exit" 태그 설정
/// </summary>
public class GuidanceManager : Agent
{
    public static GuidanceManager Instance { get; private set; }

    [Header("업데이트 설정")]
    [Tooltip("경로 재계산 주기 (초)")]
    public float updateInterval = 0.5f;

    [Header("회전 설정")]
    [Tooltip("화살표 회전 부드러움 (높을수록 빠르게 회전)")]
    public float rotationSpeed = 5f;

    [Header("디버그")]
    public bool showPathGizmos = false;

    // 내부 데이터
    private FloorArrow[] allArrows;
    private FloorArrow[] junctionArrows;
    private Transform[]  exitPoints;
    private NavMeshPath   sharedPath;

    // 각 화살표의 목표 Y각도 (Lerp용)
    private Dictionary<FloorArrow, float> targetYAngles = new Dictionary<FloorArrow, float>();

    // RL 방향 매핑 (0=북, 1=남, 2=동, 3=서)
    private static readonly Vector3[] rlDirections = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

    // ML-Agents 센서 초기화 완료 여부 (첫 Action 수신 후 true)
    public bool IsMLAgentsReady { get; private set; } = false;

    // EvacuationManager가 에피소드 종료를 알리는 플래그
    private bool shouldEndEpisode = false;

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
        if (EvacuationManager.Instance != null)
            EvacuationManager.Instance.ResetEnvironment();
    }

    void Awake()
    {
        Instance = this;
        sharedPath = new NavMeshPath();
    }

    protected override void OnEnable()
    {
        base.OnEnable(); // ← Agent를 Academy에 등록
        Instance = this;
        if (sharedPath == null) sharedPath = new NavMeshPath();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= EditorUpdate;
            UnityEditor.EditorApplication.update += EditorUpdate;
        }
#endif
    }

    protected override void OnDisable()
    {
        base.OnDisable(); // ← Agent 등록 해제
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorUpdate;
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (sharedPath == null) sharedPath = new NavMeshPath();
            CollectArrows();
            CollectExits();
            UpdateAllArrows();
        };
    }

    private double lastEditorUpdate = 0;
    private void EditorUpdate()
    {
        if (Application.isPlaying) return;
        if (this == null) return;

        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - lastEditorUpdate < 0.5) return;
        lastEditorUpdate = now;

        if (sharedPath == null) sharedPath = new NavMeshPath();
        CollectArrows();
        CollectExits();
        UpdateAllArrows();
    }
#endif

    void Start()
    {
        CollectArrows();
        CollectExits();
        Debug.Log($"[GuidanceManager] Start - Arrows: {(allArrows != null ? allArrows.Length : 0)}, Exits: {(exitPoints != null ? exitPoints.Length : 0)}");
        if (Application.isPlaying)
        {
            var academy = Unity.MLAgents.Academy.Instance;
            academy.AutomaticSteppingEnabled = true;

            // DecisionRequester 자동 추가 (없으면)
            var dr = GetComponent<Unity.MLAgents.DecisionRequester>();
            if (dr == null)
            {
                dr = gameObject.AddComponent<Unity.MLAgents.DecisionRequester>();
                dr.DecisionPeriod = 5;
                Debug.Log("[GuidanceManager] DecisionRequester 자동 추가됨 (Period=5)");
            }

            StartCoroutine(UpdateRoutine());
        }
    }

    /// <summary>씬의 모든 FloorArrow 수집 + 교차점 필터링</summary>
    public void CollectArrows()
    {
        allArrows = FindObjectsOfType<FloorArrow>();

        var junctions = new System.Collections.Generic.List<FloorArrow>();
        foreach (var arrow in allArrows)
        {
            if (arrow != null && arrow.isJunction)
                junctions.Add(arrow);
        }
        junctionArrows = junctions.ToArray();
        if (Application.isPlaying)
            Debug.Log($"[GuidanceManager] 교차점 화살표: {junctionArrows.Length}개");
    }

    /// <summary>출구 수집: isExit FloorArrow + "Exit" 태그 오브젝트</summary>
    public void CollectExits()
    {
        var exits = new List<Transform>();

        // 1) isExit=true인 FloorArrow를 출구로 사용
        if (allArrows != null)
        {
            foreach (var arrow in allArrows)
            {
                if (arrow != null && arrow.isExit)
                    exits.Add(arrow.transform);
            }
        }

        // 2) "Exit" 태그 오브젝트도 추가
        try
        {
            var exitObjects = GameObject.FindGameObjectsWithTag("Exit");
            foreach (var obj in exitObjects)
            {
                if (!exits.Contains(obj.transform))
                    exits.Add(obj.transform);
            }
        }
        catch { }

        exitPoints = exits.ToArray();

        if (exitPoints.Length == 0)
            Debug.LogWarning("[GuidanceManager] 출구 0개! FloorArrow의 isExit 체크 또는 Exit 태그를 설정하세요.");
    }

    IEnumerator UpdateRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        while (true)
        {
            UpdateAllArrows();
            yield return new WaitForSeconds(updateInterval);
        }
    }


    void Update()
    {
        if (!Application.isPlaying) return;
        if (allArrows == null) return;

        foreach (var arrow in allArrows)
        {
            if (arrow == null || arrow.isExit) continue;

            float targetY;
            if (!targetYAngles.TryGetValue(arrow, out targetY)) continue;

            Transform model = GetArrowModel(arrow.transform);
            if (model == null) continue;

            Vector3 curEuler = model.localEulerAngles;
            float newY = Mathf.LerpAngle(curEuler.y, targetY, Time.deltaTime * rotationSpeed);
            model.localEulerAngles = new Vector3(90f, newY, 0f);
        }
    }

    public void ResetArrows()
    {
        CollectArrows();
        CollectExits();
        if (allArrows != null)
        {
            foreach (var arrow in allArrows)
            {
                if (arrow != null)
                    arrow.isBlocked = false;
            }
        }
        UpdateAllArrows();
    }

    void UpdateAllArrows()
    {
        if (allArrows == null || exitPoints == null || exitPoints.Length == 0) return;
        if (sharedPath == null) sharedPath = new NavMeshPath();

        foreach (var arrow in allArrows)
        {
            if (arrow == null) continue;

            if (arrow.isExit)
            {
                arrow.isBlocked = false;
                SetArrowColor(arrow, arrow.activeColor);
                continue;
            }

            // 교차점 화살표는 RL이 제어하므로 NavMesh 계산 건너뜀
            if (arrow.isJunction) continue;

            bool pathFound = false;
            Vector3 bestDirection = Vector3.zero;
            float bestDistance = float.MaxValue;

            foreach (var exit in exitPoints)
            {
                if (exit == null) continue;

                NavMeshHit arrowHit, exitHit;
                if (!NavMesh.SamplePosition(arrow.transform.position, out arrowHit, 2f, NavMesh.AllAreas))
                    continue;
                if (!NavMesh.SamplePosition(exit.position, out exitHit, 2f, NavMesh.AllAreas))
                    continue;

                sharedPath.ClearCorners();
                if (NavMesh.CalculatePath(arrowHit.position, exitHit.position, NavMesh.AllAreas, sharedPath))
                {
                    if (sharedPath.status == NavMeshPathStatus.PathComplete && sharedPath.corners.Length >= 2)
                    {
                        // 불이 있는 경로인지 확인
                        bool hasFire = false;
                        if (FireSpreadManager.Instance != null)
                        {
                            foreach (var corner in sharedPath.corners)
                            {
                                if (FireSpreadManager.Instance.IsNodeOnFire(corner))
                                {
                                    hasFire = true;
                                    break;
                                }
                            }
                        }

                        if (!hasFire)
                        {
                            float dist = 0f;
                            for (int i = 1; i < sharedPath.corners.Length; i++)
                                dist += Vector3.Distance(sharedPath.corners[i - 1], sharedPath.corners[i]);

                            if (dist < bestDistance)
                            {
                                bestDistance = dist;
                                bestDirection = sharedPath.corners[1] - sharedPath.corners[0];
                                pathFound = true;
                            }
                        }
                    }
                }
            }

            if (pathFound)
            {
                bestDirection.y = 0f;
                if (bestDirection.sqrMagnitude > 0.001f)
                {
                    float yAngle = Quaternion.LookRotation(bestDirection.normalized, Vector3.up).eulerAngles.y;
                    float localY = yAngle - arrow.transform.eulerAngles.y;
                    targetYAngles[arrow] = localY;

                    // Scene 모드: 즉시 적용
                    if (!Application.isPlaying)
                    {
                        Transform model = GetArrowModel(arrow.transform);
                        if (model != null)
                            model.localEulerAngles = new Vector3(90f, localY, 0f);
                    }
                }

                arrow.isBlocked = false;
                SetArrowColor(arrow, arrow.activeColor);
            }
            else
            {
                arrow.isBlocked = true;
                SetArrowColor(arrow, arrow.blockedColor);
            }
        }
    }

    void SetArrowColor(FloorArrow arrow, Color color)
    {
        Transform model = GetArrowModel(arrow.transform);
        if (model == null) return;

        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (r == null) continue;
            if (Application.isPlaying)
            {
                r.material.color = color;
                r.material.SetColor("_BaseColor", color);
            }
            else
            {
                if (r.sharedMaterial == null) continue;
                r.sharedMaterial.color = color;
                r.sharedMaterial.SetColor("_BaseColor", color);
            }
        }
    }

    Transform GetArrowModel(Transform arrowRoot)
    {
        if (arrowRoot.childCount == 0) return null;
        return arrowRoot.GetChild(0);
    }

    // ─── ML-Agents 오버라이드 ───

    public override void OnEpisodeBegin()
    {
        Debug.Log("[GuidanceManager] OnEpisodeBegin!");

        // 화살표는 에피소드 사이에 바뀌지 않으므로 재수집 안 함
        // (재수집 시 타이밍 문제로 junction 수가 달라져 observation 불일치 오류 발생)
        CollectExits();

        if (allArrows != null)
        {
            foreach (var arrow in allArrows)
            {
                if (arrow != null)
                    arrow.isBlocked = false;
            }
        }
        UpdateAllArrows();

        if (junctionArrows != null)
        {
            foreach (var arrow in junctionArrows)
            {
                if (arrow != null)
                    targetYAngles.Remove(arrow);
            }
        }

        // EvacuationManager 환경 리셋 (ML-Agents가 에피소드 주도)
        if (EvacuationManager.Instance != null)
            EvacuationManager.Instance.ResetEnvironment();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Inspector Space Size와 반드시 일치해야 함
        const int EXPECTED_OBS = 10;
        int added = 0;

        // junctionArrows가 null이거나 비어있으면 먼저 수집
        if (junctionArrows == null || junctionArrows.Length == 0)
        {
            CollectArrows();
        }

        GameObject[] fires = GameObject.FindGameObjectsWithTag("Fire");

        if (junctionArrows != null && junctionArrows.Length > 0)
        {
            foreach (var junction in junctionArrows)
            {
                if (added >= EXPECTED_OBS) break;

                float minDist = 9999f;
                if (junction != null && fires != null)
                {
                    foreach (var fire in fires)
                    {
                        if (fire == null) continue;
                        float d = Vector3.Distance(junction.transform.position, fire.transform.position);
                        if (d < minDist) minDist = d;
                    }
                }
                sensor.AddObservation(minDist);
                added++;
            }
        }

        // 부족한 만큼 9999f로 패딩 (항상 정확히 EXPECTED_OBS개 보장)
        while (added < EXPECTED_OBS)
        {
            sensor.AddObservation(9999f);
            added++;
        }
    }

    /// <summary>EvacuationManager가 호출 - 다음 스텝에서 EndEpisode 실행</summary>
    public void NotifyEpisodeDone()
    {
        shouldEndEpisode = true;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        IsMLAgentsReady = true;

        // ML-Agents 스텝 사이클 안에서 에피소드 종료 처리
        if (shouldEndEpisode)
        {
            Debug.Log("[GuidanceManager] EndEpisode 호출!");
            shouldEndEpisode = false;
            EndEpisode();
            return;
        }

        if (junctionArrows == null) return;

        for (int i = 0; i < junctionArrows.Length; i++)
        {
            if (i >= actions.DiscreteActions.Length) break;
            var arrow = junctionArrows[i];
            if (arrow == null || arrow.isExit) continue;

            int actionIdx = actions.DiscreteActions[i];
            actionIdx = Mathf.Clamp(actionIdx, 0, rlDirections.Length - 1);

            Vector3 dir = rlDirections[actionIdx];
            float yAngle = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
            float localY = yAngle - arrow.transform.eulerAngles.y;
            targetYAngles[arrow] = localY;

            arrow.isBlocked = false;
            SetArrowColor(arrow, arrow.activeColor);
        }
    }

    public void RewardForEvacuation()
    {
        AddReward(1.0f);
    }

    public void PenaltyForDeath()
    {
        AddReward(-1.0f);
    }

    // ─── 기존 Gizmo ───

    void OnDrawGizmos()
    {
        if (!showPathGizmos) return;
        if (allArrows == null || exitPoints == null) return;

        var path = new NavMeshPath();
        foreach (var arrow in allArrows)
        {
            if (arrow == null || arrow.isExit) continue;

            foreach (var exit in exitPoints)
            {
                if (exit == null) continue;

                NavMeshHit aHit, eHit;
                if (!NavMesh.SamplePosition(arrow.transform.position, out aHit, 2f, NavMesh.AllAreas)) continue;
                if (!NavMesh.SamplePosition(exit.position, out eHit, 2f, NavMesh.AllAreas)) continue;

                if (NavMesh.CalculatePath(aHit.position, eHit.position, NavMesh.AllAreas, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        Gizmos.color = Color.cyan;
                        for (int i = 1; i < path.corners.Length; i++)
                            Gizmos.DrawLine(path.corners[i - 1] + Vector3.up * 0.1f,
                                            path.corners[i] + Vector3.up * 0.1f);
                    }
                }
                break;
            }
        }
    }

    /// <summary>외부에서 에피소드를 강제로 종료할 때 사용</summary>
    public void EndEpisodeNow()
    {
        EndEpisode();
    }

    public void CheckTimeout()
    {
        if (EvacuationManager.Instance == null) return;
        int totalPeople = EvacuationManager.Instance.agents.Count;
        int evacuatedCount = EvacuationManager.Instance.agents.Count(a => a.hasEvacuated);
        int remainingPeople = totalPeople - evacuatedCount;
        if (remainingPeople > 0)
        {
            AddReward(-1.0f * remainingPeople);
        }
    }
}
