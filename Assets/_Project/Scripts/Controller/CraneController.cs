using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic; // List 사용을 위해 추가
using Data;
using Network;

public class CraneController : MonoBehaviour
{
    public enum CraneType { LLC, TC, GC }

    [Header("Identity")]
    public int pierId;
    public int craneId;
    public CraneType craneType = CraneType.LLC;

    [Header("Settings")]
    [Tooltip("이 크레인이 GPS 데이터를 사용하는지 여부. (Y부두 등은 체크)")]
    public bool useGpsCoordinates = false;

    [Header("Parts")]
    public Transform craneBody;
    public Transform craneJib;
    public Transform craneTower;

    public Transform trolley;
    public Transform hook1High, hook1Low;

    [Header("Offsets")]
    public Vector3 positionOffset;
    public float bodyAngleOffset;
    public float jibAngleOffset;
    public bool useReverseRotate = false;

    // [수정] 들어오는 데이터를 모아둘 버퍼
    private List<byte> recvBuffer = new List<byte>();

    private void Start()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnPacketReceived += HandlePacket;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnPacketReceived -= HandlePacket;
    }

    private void HandlePacket(MessageId msgId, int pId, int cId, byte[] data)
    {
        // 내 ID가 아니면 무시
        if (pId != pierId || cId != craneId) return;

        // 1. 버퍼에 데이터 추가
        recvBuffer.AddRange(data);

        // 2. 버퍼 처리 (완전한 패킷이 있는지 확인)
        ProcessBuffer();
    }

    private void ProcessBuffer()
    {
        while (recvBuffer.Count > 0)
        {
            // A. GPS 모드 또는 데이터가 '$'로 시작하면 텍스트(GPS)로 간주
            if (recvBuffer[0] == 0x24) // '$'
            {
                // 개행 문자(\n, 0x0A) 찾기
                int infoIndex = recvBuffer.IndexOf(0x0A);
                if (infoIndex != -1)
                {
                    // 한 줄 추출
                    byte[] lineBytes = recvBuffer.GetRange(0, infoIndex + 1).ToArray();
                    recvBuffer.RemoveRange(0, infoIndex + 1); // 처리한 데이터 삭제

                    if (!useGpsCoordinates) useGpsCoordinates = true; // 자동 모드 전환
                    HandleGpsString(lineBytes);
                }
                else
                {
                    // 아직 줄바꿈이 안 들어옴 -> 다음 패킷 대기
                    break;
                }
            }
            // B. 바이너리 모드 (기존 7부두 등)
            else
            {
                // 바이너리 패킷 사이즈 (예: CranePoseData가 40바이트라고 가정)
                // CraneData.cs의 FromBytes 로직에 맞춰 40바이트 이상이면 처리
                int binaryPacketSize = 40;

                if (recvBuffer.Count >= binaryPacketSize)
                {
                    // 1. 혹시 중간에 껴있는 노이즈 제거 (헤더 체크 등이 없으므로 간단히 처리)
                    // 만약 GPS 모드인데 이상한 값이면 스킵
                    if (useGpsCoordinates)
                    {
                        recvBuffer.RemoveAt(0);
                        continue;
                    }

                    byte[] packet = recvBuffer.GetRange(0, binaryPacketSize).ToArray();

                    // 처리 시도
                    CranePoseData pose = CranePoseData.FromBytes(packet);
                    if (pose != null)
                    {
                        UpdateModel(pose.Position, pose.Rotation, pose.Translation, pose.Hook1);
                        recvBuffer.RemoveRange(0, binaryPacketSize);
                    }
                    else
                    {
                        // 파싱 실패 시 1바이트만 버리고 재시도 (슬라이딩)
                        recvBuffer.RemoveAt(0);
                    }
                }
                else
                {
                    // 데이터 부족 -> 대기
                    break;
                }
            }
        }
    }

    private void HandleGpsString(byte[] data)
    {
        try
        {
            string line = Encoding.ASCII.GetString(data).Trim();

            // $GPGGA 만 처리
            if (line.Contains("$GPGGA") || line.Contains("GPGGA"))
            {
                string[] parts = line.Split(',');

                // GPGGA 포맷: $GPGGA,시간,위도,N,경도,E,품질,위성수...
                // parts[2]: 위도, parts[4]: 경도
                if (parts.Length > 9 && !string.IsNullOrEmpty(parts[2]) && !string.IsNullOrEmpty(parts[4]))
                {
                    double latRaw = double.Parse(parts[2]);
                    double lonRaw = double.Parse(parts[4]);

                    // 해발고도
                    float height = 0;
                    if (!string.IsNullOrEmpty(parts[9])) float.TryParse(parts[9], out height);

                    // 도분(DDMM.MMMM) -> 도(DD.DDDD) 변환
                    double lat = (int)(latRaw / 100) + (latRaw % 100) / 60.0;
                    double lon = (int)(lonRaw / 100) + (lonRaw % 100) / 60.0;

                    // GpsSystem을 통해 유니티 좌표로 변환
                    if (GpsSystem.Instance != null)
                    {
                        Vector3 worldPos = GpsSystem.Instance.GpsToWorldPosition(lat, lon);
                        worldPos.y = height;

                        UpdateModel(worldPos, Vector3.zero, Vector3.zero, Vector3.zero);
                    }
                }
            }
        }
        catch (Exception)
        {
            // 파싱 에러 무시
        }
    }

    private void UpdateModel(Vector3 pos, Vector3 rot, Vector3 trans, Vector3 hook)
    {
        // 1. 위치 업데이트
        transform.localPosition = pos + positionOffset;

        // 2. 회전 업데이트
        if (craneType == CraneType.LLC)
        {
            if (craneJib != null)
                craneJib.localRotation = Quaternion.AngleAxis(rot.x + jibAngleOffset, Vector3.right);

            if (craneBody != null)
            {
                float angle = (rot.z) + bodyAngleOffset;
                craneBody.localRotation = Quaternion.Euler(0, angle + 180f, 0);
            }
        }
        else if (craneType == CraneType.TC)
        {
            if (craneTower != null)
            {
                float angle = rot.z + 180f;
                craneTower.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }
        else if (craneType == CraneType.GC)
        {
            if (craneBody != null) craneBody.localPosition = new Vector3(0, trans.y, 0);
            if (trolley != null) trolley.localPosition = new Vector3(0, hook.y, 0);
            if (hook1Low != null) hook1Low.localPosition = hook;
        }
    }
}