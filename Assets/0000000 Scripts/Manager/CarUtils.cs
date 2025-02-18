using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarUtils
{
    /// <summary>
    /// km/h 단위의 속도를 m/s 단위의 속도 값으로 반환한다. 
    /// </summary>
    /// <param name="speedKmH">목표 속도를 km/h 값으로 받는다. </param>
    /// <returns></returns>
    public static float ConvertKmHToMS(float speedKmH)
    {
        return speedKmH / 3.6f;
    }
}
