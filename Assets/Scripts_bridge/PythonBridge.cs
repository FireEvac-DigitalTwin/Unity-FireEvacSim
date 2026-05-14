using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Python 강화학습 소켓 서버와 통신하는 브리지
/// env_core.py의 FireEvacEnv와 소켓으로 데이터 송수신
/// </summary>
public class PythonBridge : MonoBehaviour
{
    [System.Serializable]
    public class GridInfo
    {
        public int rows;
        public int cols;
        public int[][] grid;
        public List<int[]> exit_a;
        public List<int[]> exit_b;
        public int scenario;
        public string scenario_name;
        public int n_agents;
    }

    [System.Serializable]
    public class Person
    {
        public int id;
        public int row;
        public int col;
        public float panic;
        public float speed;
    }

    [System.Serializable]
    public class StepSnapshot
    {
        public int step;
        public List<Person> people;
        public List<int[]> fire_cells;
        public List<int[]> smoke_cells;
        public int escaped;
        public int escaped_A;
        public int escaped_B;
        public int dead;
        public List<int[]> blocked_exits;
        public Dictionary<string, int> light_dirs;
    }

    [System.Serializable]
    public class ActionData
    {
        public float exit_A_cost;
        public float exit_B_cost;
        public float crowd_weight;
        public Dictionary<string, int> light_dirs;
    }

    [System.Serializable]
    public class EpisodeInfo
    {
        public string scenario_name;
        public int scenario;
        public int step;
        public int n_agents;
        public int escaped;
        public int dead;
        public int escaped_A;
        public int escaped_B;
        public int remaining;
        public float survival_rate;
        public int fire_cells;
        public List<int[]> blocked_exits;
        public float mean_panic;
    }

    [System.Serializable]
    public class ServerMessage
    {
        public string message_type;
        public GridInfo grid_info;
        public StepSnapshot initial_snapshot; // 💡 init 시점에 받는 초기 스냅샷 추가
        public StepSnapshot snapshot;
        public float exit_A_cost;
        public float exit_B_cost;
        public float crowd_weight;
        public float[] directions;
        public Dictionary<string, int> light_dirs;
        public float reward;
        public bool terminated;
        public bool truncated;
        public int step;
        public EpisodeInfo info;
        public EpisodeInfo final_info;
    }

    // 💡 유니티 -> 파이썬 요청 메시지 간소화 (observation 제거)
    [System.Serializable]
    public class ClientMessage
    {
        public string request; // "next_step" 또는 "disconnect"
    }

    // ==================== 필드 ====================
    [SerializeField] private string pythonServerHost = "127.0.0.1";
    [SerializeField] private int pythonServerPort = 5555;
    [SerializeField] private float requestInterval = 0.5f; // 💡 애니메이션 속도에 맞춰 조절 (ex: 0.5초)
    [SerializeField] private int receiveTimeout = 5000;

    private TcpClient socketClient;
    private NetworkStream networkStream;
    private bool isConnected = false;
    private bool isInitialized = false;
    private float timeSinceLastRequest = 0f;
    private int currentStep = 0;
    private bool isRequesting = false;
    private GridInfo currentGridInfo;
    private StepSnapshot currentSnapshot;
    private ActionData lastAction;

    // ==================== 이벤트 ====================
    public delegate void OnGridInfoReceived(GridInfo gridInfo);
    public delegate void OnSnapshotReceived(StepSnapshot snapshot);
    public delegate void OnActionReceived(ActionData action);
    public delegate void OnEpisodeEnd(EpisodeInfo info);
    public delegate void OnConnectionStatus(bool connected);

    public event OnGridInfoReceived GridInfoReceivedEvent;
    public event OnSnapshotReceived SnapshotReceivedEvent;
    public event OnActionReceived ActionReceivedEvent;
    public event OnEpisodeEnd EpisodeEndEvent;
    public event OnConnectionStatus ConnectionStatusEvent;

    private void Start()
    {
        Debug.Log("[PythonBridge] 초기화 중...");
        ConnectToServer();
    }

    private void Update()
    {
        if (!isConnected || !isInitialized || isRequesting)
            return;

        timeSinceLastRequest += Time.deltaTime;
        
        // 💡 지정된 인터벌마다 다음 스텝 요청 (비주얼 이동 시간을 벌어줌)
        if (timeSinceLastRequest >= requestInterval)
        {
            timeSinceLastRequest = 0f;
            RequestNextStep();
        }
    }

    // ==================== 소켓 연결 ====================

    public void ConnectToServer()
    {
        if (isConnected) return;

        try
        {
            socketClient = new TcpClient();
            socketClient.ConnectAsync(pythonServerHost, pythonServerPort).Wait(5000);

            if (socketClient.Connected)
            {
                networkStream = socketClient.GetStream();
                socketClient.SendTimeout = receiveTimeout;
                socketClient.ReceiveTimeout = receiveTimeout;
                
                isConnected = true;
                Debug.Log($"[PythonBridge] ✅ 서버 연결 성공! ({pythonServerHost}:{pythonServerPort})");
                
                ConnectionStatusEvent?.Invoke(true);
                StartCoroutine(ReceiveInitMessage());
            }
            else
            {
                Debug.LogError("[PythonBridge] ❌ 서버 연결 실패");
                isConnected = false;
                ConnectionStatusEvent?.Invoke(false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonBridge] ❌ 연결 에러: {e.Message}");
            isConnected = false;
            ConnectionStatusEvent?.Invoke(false);
        }
    }

    private IEnumerator ReceiveInitMessage()
    {
        yield return new WaitForSeconds(0.5f);
        
        try
        {
            ServerMessage message = ReceiveMessage();
            
            // 💡 서버에서 보내는 init 메시지에 grid_info와 initial_snapshot이 모두 포함되어 있음
            if (message.message_type == "init" && message.grid_info != null)
            {
                currentGridInfo = message.grid_info;
                currentStep = 0;
                
                isInitialized = true;

                Debug.Log($"[PythonBridge] ✅ 환경 초기화 메시지 수신!");
                Debug.Log($"  그리드: {currentGridInfo.rows}x{currentGridInfo.cols} | 시나리오: {currentGridInfo.scenario_name}");

                // 1. 그리드 생성 이벤트 호출
                GridInfoReceivedEvent?.Invoke(currentGridInfo);

                // 2. 초기 캐릭터 위치 배치를 위한 스냅샷 이벤트 호출
                if (message.initial_snapshot != null)
                {
                    currentSnapshot = message.initial_snapshot;
                    SnapshotReceivedEvent?.Invoke(currentSnapshot);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonBridge] ❌ 초기화 메시지 수신 실패: {e.Message}");
        }
    }

    // ==================== 메시지 송수신 ====================

    private ServerMessage ReceiveMessage()
    {
        try
        {
            byte[] lengthBuffer = new byte[4];
            int bytesRead = networkStream.Read(lengthBuffer, 0, 4);
            
            if (bytesRead == 0) throw new Exception("서버 연결이 끊어졌습니다");

            if (BitConverter.IsLittleEndian) System.Array.Reverse(lengthBuffer);
            
            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            byte[] messageBuffer = new byte[messageLength];
            int totalRead = 0;
            
            while (totalRead < messageLength)
            {
                int read = networkStream.Read(messageBuffer, totalRead, messageLength - totalRead);
                if (read == 0) throw new Exception("메시지 수신 중 연결 끊김");
                totalRead += read;
            }

            string jsonStr = Encoding.UTF8.GetString(messageBuffer);
            return JsonConvert.DeserializeObject<ServerMessage>(jsonStr);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonBridge] ❌ 메시지 수신 에러: {e.Message}");
            DisconnectFromServer();
            throw;
        }
    }

    private void SendMessage(ClientMessage message)
    {
        try
        {
            string jsonStr = JsonConvert.SerializeObject(message);
            byte[] messageBytes = Encoding.UTF8.GetBytes(jsonStr);
            byte[] lengthBytes = BitConverter.GetBytes(messageBytes.Length);
            
            if (BitConverter.IsLittleEndian) System.Array.Reverse(lengthBytes);

            networkStream.Write(lengthBytes, 0, 4);
            networkStream.Write(messageBytes, 0, messageBytes.Length);
            networkStream.Flush();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonBridge] ❌ 메시지 전송 에러: {e.Message}");
            DisconnectFromServer();
        }
    }

    // ==================== 요청/응답 ====================

    public void RequestNextStep()
    {
        if (!isConnected || !isInitialized) return;
        StartCoroutine(RequestNextStepCoroutine());
    }

    private IEnumerator RequestNextStepCoroutine()
    {
        isRequesting = true; // 통신 락(Lock)

        // 💡 심플하게 다음 스텝을 달라고 서버에 핑을 보냄
        SendMessage(new ClientMessage { request = "next_step" });
        
        ServerMessage response = null;
        try
        {
            response = ReceiveMessage();
        }
        catch (Exception)
        {
            isRequesting = false;
            yield break;
        }

        if (response != null && response.message_type == "step_snapshot")
        {
            currentSnapshot = response.snapshot;
            currentStep = response.step;
            
            lastAction = new ActionData
            {
                exit_A_cost = response.exit_A_cost,
                exit_B_cost = response.exit_B_cost,
                crowd_weight = response.crowd_weight,
                light_dirs = response.light_dirs ?? new Dictionary<string, int>()
            };

            SnapshotReceivedEvent?.Invoke(currentSnapshot);
            ActionReceivedEvent?.Invoke(lastAction);

            PrintStepInfo(response);

            // 💡 에피소드 종료 처리
            if (response.terminated || response.truncated)
            {
                isInitialized = false; 
                try
                {
                    ServerMessage endMessage = ReceiveMessage();
                    if (endMessage != null && endMessage.message_type == "episode_end")
                    {
                        EpisodeEndEvent?.Invoke(endMessage.final_info);
                        PrintEpisodeEnd(endMessage.final_info);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PythonBridge] 에피소드 종료 메시지 수신 실패: {e.Message}");
                }
                DisconnectFromServer(); 
            }
        }
        
        isRequesting = false; // 통신 락(Lock) 해제
    }

    private void PrintStepInfo(ServerMessage msg)
    {
        if (msg.step % 10 == 0 || msg.terminated || msg.truncated) // 로그가 너무 많아지는 걸 방지
        {
            string output = $"[Step {msg.step}] 보상: {msg.reward:+0.0} | ";
            if (msg.snapshot != null)
            {
                output += $"생존 {msg.snapshot.escaped} / 사망 {msg.snapshot.dead} / 남은인원 {msg.snapshot.people.Count}";
            }
            Debug.Log(output);
        }
    }

    private void PrintEpisodeEnd(EpisodeInfo info)
    {
        Debug.Log($"\n🏁 [에피소드 종료] 시나리오: {info.scenario_name} | 생존율: {info.survival_rate:P1}\n");
    }

    // ==================== 유틸리티 ====================

    public bool IsConnected => isConnected;
    public bool IsInitialized => isInitialized;
    public GridInfo CurrentGridInfo => currentGridInfo;
    public StepSnapshot CurrentSnapshot => currentSnapshot;

    private void OnApplicationQuit()
    {
        if (isConnected)
        {
            SendMessage(new ClientMessage { request = "disconnect" });
            DisconnectFromServer();
        }
    }

    public void DisconnectFromServer()
    {
        if (isConnected)
        {
            try
            {
                networkStream?.Close();
                socketClient?.Close();
                isConnected = false;
                isInitialized = false;
                Debug.Log("[PythonBridge] 서버 연결 해제");
                ConnectionStatusEvent?.Invoke(false);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PythonBridge] 연결 해제 에러: {e.Message}");
            }
        }
    }
}