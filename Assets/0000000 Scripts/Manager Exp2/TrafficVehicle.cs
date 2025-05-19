// TrafficVehicle.cs
using UnityEngine;

public class TrafficVehicle : MonoBehaviour
{
    private float speedKmh;
    public int laneIndex { get; private set; }

    /// <summary>
    /// TrafficManager에서 속도를 세팅한다.
    /// </summary>
    public void SetSpeed(float kmh)
    {
        speedKmh = kmh;
    }

    /// <summary>
    /// TrafficManager에서 차선 인덱스를 세팅한다.
    /// </summary>
    public void SetLane(int lane)
    {
        laneIndex = lane;
    }

    /// <summary>
    /// 현재 속도를 가져온다.
    /// </summary>
    public float GetSpeed()
    {
        return speedKmh;
    }

    void Update()
    {
        // km/h → m/s
        float speedMs = speedKmh * (1000f / 3600f);
        transform.position += Vector3.forward * speedMs * Time.deltaTime;
    }
}