using UnityEngine;
using Data;
using Network;

public class CraneController : MonoBehaviour
{
    public enum CraneType { LLC, TC, GC }

    [Header("Identity")]
    public int pierId;
    public int craneId;
    public CraneType craneType = CraneType.LLC;

    [Header("Parts")]
    public Transform craneBody;  // LLC 몸통, GC 타워 등
    public Transform craneJib;   // 붐/지브
    public Transform craneTower; // TC 타워

    // GC용 파츠
    public Transform trolley;
    public Transform hook1High, hook1Low;
    // (필요 시 hook2, hook3 추가)

    [Header("Offsets")]
    public Vector3 positionOffset;
    public float bodyAngleOffset;
    public float jibAngleOffset;
    public bool useReverseRotate = false;

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
        if (pId != pierId || cId != craneId) return;

        if (msgId == MessageId.CraneAttitude) // ID 1
        {
            CranePoseData pose = CranePoseData.FromBytes(data);
            if (pose != null) UpdateModel(pose);
        }
    }

    private void UpdateModel(CranePoseData pose)
    {
        // 1. 위치 업데이트 (MySocket.cs: transform.localPosition = currentPos + offset)
        // ID 1의 데이터는 이미 Unity Local Coordinates 형태입니다.
        transform.localPosition = pose.Position + positionOffset;

        // 2. 회전 업데이트 (CraneParts.cs 로직 참조)
        // rotation: x(JibAngle), z(Azimuth/BodyAngle) - MySocket에서 이미 부호 반전됨

        if (craneType == CraneType.LLC)
        {
            // 지브 각도 (X축 회전)
            if (craneJib != null)
            {
                // MySocket: SetJibAngle(-rot.x) -> 이미 pose.Rotation에 -가 적용되어 있음
                // 하지만 MySocket.cs를 보면 data parsing 때 -를 붙이고, UpdateModels에서 또 -를 붙이기도 함.
                // MySocket.cs Parsing: rot = new Vector3(-buf..);
                // MySocket.cs Update: SetJibAngle(-rot.x);
                // 결론: 원본 값 양수화.
                // 여기서는 pose.Rotation.x를 그대로 쓰되, 필요 시 부호 조정
                craneJib.localRotation = Quaternion.AngleAxis(pose.Rotation.x + jibAngleOffset, Vector3.right);
            }

            // 몸통 회전 (Z축 회전 -> Y축 회전으로 변환)
            // MySocket: SetCraneRotate(-rot.z + 180)
            if (craneBody != null)
            {
                // Y축 회전으로 적용
                float angle = (pose.Rotation.z) + bodyAngleOffset;
                // 180도 보정은 상황에 따라 다를 수 있으나 MySocket을 따름
                craneBody.localRotation = Quaternion.Euler(0, angle + 180f, 0);
            }
        }
        else if (craneType == CraneType.TC)
        {
            if (craneTower != null)
            {
                float angle = pose.Rotation.z + 180f;
                craneTower.localRotation = Quaternion.Euler(0, 0, angle); // TC는 Z축 회전을 그대로 쓰는 경우가 있음 (2D/3D 차이)
                // 만약 3D 타워라면 Quaternion.Euler(0, angle, 0) 일 것임. 
                // MySocket의 TC 코드는 Z축 회전을 사용했음 (2D 기반일 가능성). 
                // 3D라면: craneTower.localRotation = Quaternion.Euler(0, angle, 0);
            }
        }
        else if (craneType == CraneType.GC)
        {
            // GC는 Translation과 Hook 위치 사용
            // MySocket: SetGCTowerTransform(new Vector3(0, trans.y, 0), ...)
            if (craneBody != null) // Operation Room or Main Body
            {
                craneBody.localPosition = new Vector3(0, pose.Translation.y, 0);
            }
            if (trolley != null)
            {
                trolley.localPosition = new Vector3(0, pose.Hook1.y, 0); // 예시 매핑
            }
            // Hook 위치 적용
            if (hook1Low != null) hook1Low.localPosition = pose.Hook1;
        }
    }
}