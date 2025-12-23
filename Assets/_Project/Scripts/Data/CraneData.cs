using UnityEngine;
using System;
using Network;

namespace Data
{
    // ID 1: 크레인 자세 (120 bytes)
    public class CranePoseData
    {
        public Vector3 Position;     // Unity Local Position
        public Vector3 Rotation;     // Rotation (Angle)
        public Vector3 Translation;  // For GC
        public Vector3 Hook1;
        public Vector3 Hook2;
        public Vector3 Hook3;

        public static CranePoseData FromBytes(byte[] buffer)
        {
            if (buffer.Length < 120) return null;

            CranePoseData data = new CranePoseData();

            // MySocket.cs: position = new Vector3(buf[0], -buf[4], buf[8])
            data.Position = new Vector3(
                BitConverter.ToSingle(buffer, 0),  // X는 그대로
                BitConverter.ToSingle(buffer, 8),  // 서버의 Z(높이 40) -> 유니티의 Y(높이)로
                BitConverter.ToSingle(buffer, 4)   // 서버의 Y(거리 -1355) -> 유니티의 Z(깊이)로
            );

            // MySocket.cs: rotation = new Vector3(-buf[12], -buf[16], -buf[20])
            data.Rotation = new Vector3(
                -BitConverter.ToSingle(buffer, 12),
                -BitConverter.ToSingle(buffer, 16),
                -BitConverter.ToSingle(buffer, 20)
            );

            // Translation (24, 28, 32)
            data.Translation = new Vector3(
                BitConverter.ToSingle(buffer, 24),
                BitConverter.ToSingle(buffer, 28),
                BitConverter.ToSingle(buffer, 32)
            );

            // Hooks (36~)
            data.Hook1 = ReadVector3(buffer, 36);
            data.Hook2 = ReadVector3(buffer, 48);
            data.Hook3 = ReadVector3(buffer, 60);

            return data;
        }

        private static Vector3 ReadVector3(byte[] buf, int offset)
        {
            return new Vector3(
                BitConverter.ToSingle(buf, offset),
                BitConverter.ToSingle(buf, offset + 4),
                BitConverter.ToSingle(buf, offset + 8)
            );
        }
    }

    // ID 0: 포인트 클라우드
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
            // float timestamp = BitConverter.ToSingle(buffer, 4);

            int count = (int)data.NumPoints;
            if (count > 650000) count = 650000; // Safety limit from MySocket.cs

            data.Points = new Vector3[count];
            data.Colors = new Color[count];

            int offset = 8;
            for (int i = 0; i < count; i++)
            {
                if (offset + 24 > buffer.Length) break;

                // MySocket.cs: new Vector3(buf[0], buf[8], buf[4]) -> (X, Z, Y) 순서로 보임
                // Unity는 Y-Up, 서버 데이터가 Z-Up이라면 (X, Z, Y)로 변환하는 것이 일반적
                float x = BitConverter.ToSingle(buffer, offset + 0);
                float z = BitConverter.ToSingle(buffer, offset + 8); // MySocket 순서: 0, 8, 4
                float y = BitConverter.ToSingle(buffer, offset + 4);

                data.Points[i] = new Vector3(x, y, z);

                // Colors
                float r = BitConverter.ToSingle(buffer, offset + 12);
                float g = BitConverter.ToSingle(buffer, offset + 16);
                float b = BitConverter.ToSingle(buffer, offset + 20);
                data.Colors[i] = new Color(r, g, b, 1f);

                offset += 24;
            }

            return data;
        }
    }
}