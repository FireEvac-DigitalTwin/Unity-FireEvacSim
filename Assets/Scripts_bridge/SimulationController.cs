using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전체 시뮬레이션을 제어하는 중앙 컨트롤러
/// </summary>
public class SimulationController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text agentCountText;
    [SerializeField] private Text fireCountText;
    [SerializeField] private Text stepCountText;

    [Header("참조")]
    [SerializeField] private PythonBridge pythonBridge;
    [SerializeField] private AgentSynchronizer agentSync;
    [SerializeField] private FireSynchronizer fireSyncer;
    [SerializeField] private ArrowSynchronizer arrowSync;

    private int currentStep = 0;

    private void Start()
    {
        Debug.Log("[SimulationController] 시작...");

        // 1️⃣ PythonBridge 찾기
        if (pythonBridge == null)
        {
            pythonBridge = GetComponent<PythonBridge>();
        }
        
        if (pythonBridge == null)
        {
            pythonBridge = FindObjectOfType<PythonBridge>();
        }

        if (pythonBridge == null)
        {
            Debug.LogError("❌ PythonBridge를 찾을 수 없습니다!");
            return;  // ⭐️ 여기서 조기 종료
        }

        Debug.Log("✅ PythonBridge 찾음");

        // 2️⃣ Synchronizer들 찾기
        if (agentSync == null)
        {
            agentSync = GetComponent<AgentSynchronizer>();
            if (agentSync == null)
                agentSync = FindObjectOfType<AgentSynchronizer>();
        }

        if (fireSyncer == null)
        {
            fireSyncer = GetComponent<FireSynchronizer>();
            if (fireSyncer == null)
                fireSyncer = FindObjectOfType<FireSynchronizer>();
        }

        if (arrowSync == null)
        {
            arrowSync = GetComponent<ArrowSynchronizer>();
            if (arrowSync == null)
                arrowSync = FindObjectOfType<ArrowSynchronizer>();
        }

        Debug.Log($"✅ AgentSynchronizer: {(agentSync != null ? "찾음" : "없음")}");
        Debug.Log($"✅ FireSynchronizer: {(fireSyncer != null ? "찾음" : "없음")}");
        Debug.Log($"✅ ArrowSynchronizer: {(arrowSync != null ? "찾음" : "없음")}");

        // 3️⃣ 이벤트 구독
        if (pythonBridge != null)
        {
            pythonBridge.ConnectionStatusEvent += OnConnectionStatusChanged;
            pythonBridge.SnapshotReceivedEvent += OnSnapshotReceived;
            pythonBridge.EpisodeEndEvent += OnEpisodeEnd;
            Debug.Log("✅ 이벤트 구독 완료");
        }

        Debug.Log("[SimulationController] 준비 완료!");
    }

    private void Update()
    {
        UpdateUI();
    }

    private void OnConnectionStatusChanged(bool connected)
    {
        Debug.Log($"[SimulationController] 연결 상태: {connected}");
        
        if (statusText != null)
        {
            statusText.text = connected ? "✅ Connected" : "❌ Disconnected";
            statusText.color = connected ? Color.green : Color.red;
        }
    }

    private void OnSnapshotReceived(PythonBridge.StepSnapshot snapshot)
    {
        if (snapshot != null)
        {
            currentStep = snapshot.step;
            Debug.Log($"[SimulationController] Step {currentStep} 수신");
        }
    }

    private void OnEpisodeEnd(PythonBridge.EpisodeInfo info)
    {
        Debug.Log($"[SimulationController] 에피소드 종료 - 생존율: {info.survival_rate:P1}");
        
        if (statusText != null)
        {
            statusText.text = $"🏁 Episode End\nSurvival Rate: {info.survival_rate:P1}";
        }
    }

    private void UpdateUI()
    {
        if (stepCountText != null)
            stepCountText.text = $"Step: {currentStep}";

        if (agentCountText != null && agentSync != null)
            agentCountText.text = $"Agents: {agentSync.GetAgentCount()}";

        if (fireCountText != null && fireSyncer != null)
            fireCountText.text = $"Fire: {fireSyncer.GetFireCount()}";
    }

    private void OnDestroy()
    {
        if (pythonBridge != null)
        {
            pythonBridge.ConnectionStatusEvent -= OnConnectionStatusChanged;
            pythonBridge.SnapshotReceivedEvent -= OnSnapshotReceived;
            pythonBridge.EpisodeEndEvent -= OnEpisodeEnd;
        }
    }
}