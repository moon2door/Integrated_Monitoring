using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json; // 패키지 설치 필요

namespace Network
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Config")]
        public string jsonFileName = "IntegratedMonitoringInterface.json";

        // 여러 개의 연결을 관리할 리스트
        private List<CraneConnector> connectors = new List<CraneConnector>();

        // 메인 스레드 큐
        private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

        // 외부(CraneController)에서 구독할 이벤트
        // 매개변수: MessageId(여기선 1로 고정), PierID, CraneID, BodyData
        public event Action<MessageId, int, int, byte[]> OnPacketReceived;

        private void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else { Destroy(gameObject); }
        }

        private void Start()
        {
            LoadConfigAndConnect();
        }

        private void Update()
        {
            // 백그라운드 스레드에서 받은 데이터를 메인 스레드에서 이벤트 발생
            while (mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        private void OnApplicationQuit()
        {
            foreach (var connector in connectors)
            {
                connector.Stop();
            }
        }

        private void LoadConfigAndConnect()
        {
            // JSON 파일 경로는 Assets/StreamingAssets 폴더를 권장합니다.
            string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);

            if (!File.Exists(path))
            {
                // 에디터 루트 경로 등 다른 경로 체크 (테스트용)
                path = Path.Combine(Environment.CurrentDirectory, jsonFileName);
                if (!File.Exists(path))
                {
                    Debug.LogError($"JSON 파일을 찾을 수 없습니다: {path}");
                    return;
                }
            }

            try
            {
                string jsonContent = File.ReadAllText(path);

                // JSON 구조가 "이름": { "routerIP":..., "routerPort":... } 형태임
                var configData = JsonConvert.DeserializeObject<Dictionary<string, CraneConfigItem>>(jsonContent);

                foreach (var item in configData)
                {
                    string craneName = item.Key; // 예: "7_GC5"
                    string ip = item.Value.routerIP;
                    int port = item.Value.routerPort;

                    // 설정 파일 내 불필요한 항목 제외
                    if (craneName == "DB" || craneName == "Test" || craneName.Contains("Unity")) continue;

                    // 이름에서 ID 추출 (규칙에 따라 작성 필요)
                    (int pId, int cId) = ParseIds(craneName);

                    // 커넥터 생성 및 시작
                    CraneConnector connector = new CraneConnector(pId, cId, ip, port, craneName, OnDataFromConnector);

                    connector.StartConnection();
                    connectors.Add(connector);
                }
                Debug.Log($"총 {connectors.Count}개의 크레인 연결 시도 시작.");
            }
            catch (Exception e)
            {
                Debug.LogError($"설정 로드 실패: {e.Message}");
            }
        }

        // 각 커넥터 스레드에서 호출되는 콜백
        private void OnDataFromConnector(int pId, int cId, byte[] data)
        {
            // 메인 스레드 큐에 넣기
            mainThreadActions.Enqueue(() =>
            {
                // MessageId.CraneAttitude (1번)으로 가정하고 이벤트 전송
                OnPacketReceived?.Invoke(MessageId.CraneAttitude, pId, cId, data);
            });
        }

        private (int, int) ParseIds(string name)
        {
            int pId = 0;
            int cId = 0;

            try
            {
                string[] parts = name.Split('_');
                string prefix = parts[0].ToUpper(); // 대문자로 변환 ("y" -> "Y")

                // PierUtility.cs 정의에 따른 매핑 (인덱스 순서)
                switch (prefix)
                {
                    case "7": pId = 0; break;
                    case "J": pId = 1; break;
                    case "K": pId = 2; break;
                    case "HAN": pId = 3; break;
                    case "6": pId = 4; break;
                    case "G2": pId = 5; break;
                    case "G3": pId = 6; break;
                    case "G4": pId = 7; break;
                    case "0D": pId = 8; break;
                    case "Y": pId = 9; break; // Y = ENI = 9번
                    case "ENI": pId = 9; break;
                    default:
                        // 예외 케이스: 숫자로 시작하면 그대로 파싱 시도 (혹시 모를 대비)
                        int.TryParse(System.Text.RegularExpressions.Regex.Match(prefix, @"\d+").Value, out pId);
                        break;
                }

                // --- Crane ID 파싱 ---
                // 예: JIB5 -> 5, TC1 -> 1
                if (parts.Length > 1)
                {
                    string cranePart = parts[1];
                    string numberOnly = System.Text.RegularExpressions.Regex.Replace(cranePart, @"[^0-9]", "");
                    int.TryParse(numberOnly, out cId);
                }
            }
            catch
            {
                Debug.LogWarning($"ID 파싱 실패: {name}");
            }

            return (pId, cId);
        }
    }

    // JSON 파싱용 클래스
    public class CraneConfigItem
    {
        public string routerIP;
        public int routerPort;
    }
}