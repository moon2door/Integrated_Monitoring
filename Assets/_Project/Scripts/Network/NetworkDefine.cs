namespace Network
{
    public enum MessageId
    {
        None = -1,
        PointCloud = 0,         // 점 데이터
        CraneAttitude = 1,      // 크레인 자세 (위치, 회전, 훅 등 - 120byte)
        CraneStatus = 2,        // 장비 상태 (정상, 고장 등)
        Distance = 3,           // 크레인 간 거리
        CollisionLog = 4,       // 충돌 이력
        OperatorInfo = 6,       // 운전자 정보
        SystemStatus = 7,       // 풍속, 센서 상태 등
        OperationHistory = 8,   // 작업 이력

        CooperationMessage = 9,
        RequestCooperationList = 10,
        CooperationAccept = 11,
        CooperationList = 12,         // 협업 리스트
        PlcInfo = 13,
        RequestAddUser = 14,

        RequestLogin = 15,            // 송신 (로그인 요청)
        ReplyLogin = 15,              // 수신 (로그인 응답 - ID 공유)

        CooperationRemove = 16,
        RequestLogPlay = 17,
        IndicateLogPlay = 18
    }
}