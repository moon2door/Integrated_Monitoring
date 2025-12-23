using UnityEngine;

public class GpsSystem : MonoBehaviour
{
    public static GpsSystem Instance { get; private set; }

    [Header("Origin")]
    public double originLat = 34.90235;
    public double originLon = 128.59721;

    private const double EarthRadius = 6371000.0;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    // 서버 데이터가 아닌, 별도의 GPS 데이터를 받았을 때만 사용
    public Vector3 GpsToWorldPosition(double lat, double lon)
    {
        double latRad = lat * Mathf.Deg2Rad;
        double lonRad = lon * Mathf.Deg2Rad;
        double originLatRad = originLat * Mathf.Deg2Rad;
        double originLonRad = originLon * Mathf.Deg2Rad;

        double x = EarthRadius * (lonRad - originLonRad) * System.Math.Cos(originLatRad);
        double z = EarthRadius * (latRad - originLatRad);

        return new Vector3((float)x, 0f, (float)z);
    }
}