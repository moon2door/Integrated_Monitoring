using System;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Network
{
    public class CraneConnector
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private bool isRunning;

        public int PierID { get; private set; }
        public int CraneID { get; private set; }
        public string Ip { get; private set; }
        public int Port { get; private set; }

        private string craneName;
        private Action<int, int, byte[]> onDataReceived;

        public CraneConnector(int pierId, int craneId, string ip, int port, string name, Action<int, int, byte[]> callback)
        {
            this.PierID = pierId;
            this.CraneID = craneId;
            this.Ip = ip;
            this.Port = port;
            this.craneName = name;
            this.onDataReceived = callback;
        }

        public void StartConnection()
        {
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        public void Stop()
        {
            isRunning = false;
            if (stream != null) stream.Close();
            if (client != null) client.Close();
            if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
        }

        private void ReceiveLoop()
        {
            // 수신 버퍼 크기 넉넉하게 잡기
            byte[] receiveBuffer = new byte[4096];

            while (isRunning)
            {
                try
                {
                    if (client == null || !client.Connected)
                    {
                        Connect();
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (stream.CanRead)
                    {
                        // [수정] 강제로 길이를 맞추지 않고, 들어온 만큼만 읽습니다.
                        int bytesRead = stream.Read(receiveBuffer, 0, receiveBuffer.Length);

                        if (bytesRead > 0)
                        {
                            // 실제 받은 만큼만 잘라서 전송
                            byte[] validData = new byte[bytesRead];
                            Array.Copy(receiveBuffer, validData, bytesRead);

                            // 로그는 너무 많으면 성능 저하되므로 필요시 주석 해제
                            // Debug.Log($"[{craneName}] Recv {bytesRead} bytes");

                            onDataReceived?.Invoke(PierID, CraneID, validData);
                        }
                        else
                        {
                            // 0바이트 읽힘 = 연결 끊김
                            client.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[{craneName}] Receive Error: {e.Message}");
                    if (client != null) client.Close();
                    Thread.Sleep(1000);
                }
            }
        }

        private void Connect()
        {
            try
            {
                client = new TcpClient();
                // 타임아웃 1초 설정
                var result = client.BeginConnect(Ip, Port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(1000, true);

                if (success)
                {
                    client.EndConnect(result);
                    stream = client.GetStream();
                    Debug.Log($"[Connection] '{craneName}' 연결 성공 ({Ip}:{Port})");
                }
                else
                {
                    client.Close();
                }
            }
            catch { }
        }
    }
}