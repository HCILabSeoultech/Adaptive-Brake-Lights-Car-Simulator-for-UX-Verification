using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StandardBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(List<GameObject> leds, float intensity)
    {
        Color lightColor = intensity > 0 ? Color.red : Color.black;

        foreach (var led in leds)
        {
            led.GetComponent<Renderer>().material.color = lightColor;
        }

        yield break; // 코루틴이지만 반복 동작이 없기 때문에 즉시 종료
    }
}