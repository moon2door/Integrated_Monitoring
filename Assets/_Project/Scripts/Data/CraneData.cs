using UnityEngine;
using System;

namespace Data
{
    public class CranePoseData
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Translation;
        public Vector3 Hook1;
        public Vector3 Hook2;
        public Vector3 Hook3;

        public static CranePoseData FromBytes(byte[] buffer)
        {
            if (buffer.Length < 40) return null;

            CranePoseData data = new CranePoseData();

            // [중요] 오프셋 설정 (일단 4로 시도, 로그 보고 수정 예정)
            int offset = 4;

            // 위치 파싱
            float x = ReadFloatBE(buffer, offset + 0);
            float y = ReadFloatBE(buffer, offset + 8);
            float z = ReadFloatBE(buffer, offset + 4);

            // [안전장치] 값이 비정상적(NaN, Infinity, 너무 큼)이면 0으로 초기화
            if (IsInvalid(x) || IsInvalid(y) || IsInvalid(z))
            {
                data.Position = Vector3.zero;
            }
            else
            {
                // 서버 좌표(X, Z, Y) -> 유니티(X, Y, Z) 매핑 시도
                data.Position = new Vector3(x, z, y);
            }

            // 회전 파싱
            float rx = ReadFloatBE(buffer, offset + 12);
            float ry = ReadFloatBE(buffer, offset + 16);
            float rz = ReadFloatBE(buffer, offset + 20);

            if (IsInvalid(rx) || IsInvalid(ry) || IsInvalid(rz))
            {
                data.Rotation = Vector3.zero;
            }
            else
            {
                data.Rotation = new Vector3(rx, ry, rz);
            }

            return data;
        }

        // 값 검증 함수
        private static bool IsInvalid(float val)
        {
            return float.IsNaN(val) || float.IsInfinity(val) || Math.Abs(val) > 1000000f;
        }

        private static float ReadFloatBE(byte[] data, int startIndex)
        {
            if (startIndex + 4 > data.Length) return 0f;

            byte[] temp = new byte[4];
            Array.Copy(data, startIndex, temp, 0, 4);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(temp);
            }

            return BitConverter.ToSingle(temp, 0);
        }
    }

    public class PointCloudData
    {
        public uint NumPoints;
        public Vector3[] Points;
        public Color[] Colors;

        public static PointCloudData FromBytes(byte[] buffer)
        {
            if (buffer.Length < 8) return null;
            PointCloudData data = new PointCloudData();
            data.NumPoints = BitConverter.ToUInt32(buffer, 0);
            return data;
        }
    }
}