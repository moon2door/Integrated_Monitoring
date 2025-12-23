using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Network;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("Connection Settings")]
    [SerializeField] private string serverIp = "127.0.0.1";
    [SerializeField] private int serverPort = 15000;

    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isRunning;

    // 메인 스레드 큐
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    // 이벤트: (MessageId, PierID, CraneID, BodyData)
    public event Action<MessageId, int, int, byte[]> OnPacketReceived;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        ConnectToServer();
    }

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out var action)) action?.Invoke();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void ConnectToServer()
    {
        if (client != null && client.Connected) return;
        isRunning = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void Disconnect()
    {
        isRunning = false;
        if (stream != null) stream.Close();
        if (client != null) client.Close();
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
    }

    private void ReceiveLoop()
    {
        byte[] headerBuffer = new byte[12]; // 헤더 고정 12바이트

        while (isRunning)
        {
            try
            {
                if (client == null || !client.Connected || stream == null)
                {
                    AttemptConnection();
                    Thread.Sleep(1000);
                    continue;
                }

                // 1. 헤더 읽기
                if (!ReadExactly(stream, headerBuffer, 12))
                {
                    client.Close();
                    continue;
                }

                // MySocket.cs 파싱 로직 (Little Endian)
                int msgId = BitConverter.ToInt32(headerBuffer, 0);
                int pierId = BitConverter.ToInt32(headerBuffer, 4);
                int craneId = BitConverter.ToInt32(headerBuffer, 8);

                MessageId msgEnum = (MessageId)msgId;
                byte[] bodyBuffer = null;

                // 2. 바디 읽기
                if (msgEnum == MessageId.PointCloud) // ID 0
                {
                    // [개수(4)][Timestamp(4)] 먼저 읽기
                    byte[] infoBytes = new byte[8];
                    if (!ReadExactly(stream, infoBytes, 8)) break;

                    uint numPoints = BitConverter.ToUInt32(infoBytes, 0);
                    int pointDataSize = (int)numPoints * 24; // (x,y,z, r,g,b) = 24byte

                    bodyBuffer = new byte[8 + pointDataSize];
                    Array.Copy(infoBytes, 0, bodyBuffer, 0, 8);

                    if (pointDataSize > 0)
                        if (!ReadExactly(stream, bodyBuffer, pointDataSize, 8)) break;
                }
                else if (msgEnum == MessageId.Distance) // ID 3
                {
                    byte[] infoBytes = new byte[4];
                    if (!ReadExactly(stream, infoBytes, 4)) break;

                    uint numDist = BitConverter.ToUInt32(infoBytes, 0);
                    int dataSize = (int)numDist * 32;

                    bodyBuffer = new byte[4 + dataSize];
                    Array.Copy(infoBytes, 0, bodyBuffer, 0, 4);

                    if (dataSize > 0)
                        if (!ReadExactly(stream, bodyBuffer, dataSize, 4)) break;
                }
                else if (msgEnum == MessageId.CooperationList) // ID 12
                {
                    byte[] infoBytes = new byte[4];
                    if (!ReadExactly(stream, infoBytes, 4)) break;

                    int fixedSize = 1640; // 164 * 10
                    bodyBuffer = new byte[4 + fixedSize];
                    Array.Copy(infoBytes, 0, bodyBuffer, 0, 4);

                    if (!ReadExactly(stream, bodyBuffer, fixedSize, 4)) break;
                }
                else
                {
                    int bodySize = GetFixedBodySize(msgEnum);
                    if (bodySize > 0)
                    {
                        bodyBuffer = new byte[bodySize];
                        if (!ReadExactly(stream, bodyBuffer, bodySize)) break;
                    }
                }

                // 3. 메인 스레드로 이벤트 전달
                if (bodyBuffer != null)
                {
                    var finalBody = bodyBuffer;
                    mainThreadActions.Enqueue(() =>
                    {
                        OnPacketReceived?.Invoke(msgEnum, pierId, craneId, finalBody);
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Socket Error: {e.Message}");
                if (client != null) client.Close();
            }
        }
    }

    private bool ReadExactly(NetworkStream stream, byte[] buffer, int size, int offset = 0)
    {
        int totalRead = 0;
        while (totalRead < size)
        {
            int read = stream.Read(buffer, offset + totalRead, size - totalRead);
            if (read == 0) return false;
            totalRead += read;
        }
        return true;
    }

    private void AttemptConnection()
    {
        try
        {
            if (client != null) client.Close();
            client = new TcpClient();
            var result = client.BeginConnect(serverIp, serverPort, null, null);
            if (result.AsyncWaitHandle.WaitOne(1000, true))
            {
                client.EndConnect(result);
                stream = client.GetStream();
                Debug.Log($"Connected to {serverIp}:{serverPort}");
            }
            else client.Close();
        }
        catch { /* Ignored */ }
    }

    private int GetFixedBodySize(MessageId id)
    {
        switch (id)
        {
            case MessageId.CraneAttitude: return 120;
            case MessageId.CraneStatus: return 256;
            case MessageId.CollisionLog: return 64092;
            case MessageId.SystemStatus: return 132;
            case MessageId.OperationHistory: return 5860;
            case MessageId.PlcInfo: return 172;
            case MessageId.ReplyLogin: return 36;
            case MessageId.IndicateLogPlay: return 72;
            default: return 0;
        }
    }
}