using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(List<GameObject> leds, float intensity)
    {
        int activeLEDs = Mathf.RoundToInt(intensity * leds.Count);

        for (int i = 0; i < leds.Count; i++)
        {
            leds[i].SetActive(i < activeLEDs);
        }

        yield break; // 단발성 동작이므로 즉시 종료
    }
}