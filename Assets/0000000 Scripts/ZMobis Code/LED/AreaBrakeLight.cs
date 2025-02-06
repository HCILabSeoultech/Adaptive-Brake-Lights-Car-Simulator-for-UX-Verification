using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(List<GameObject> leds, float intensity)
    {
        int activeLEDs = Mathf.RoundToInt(1 * leds.Count);
        DeActivateLighting(leds);
        ;

        for (int i = 0; i < leds.Count; i++)
        {
            leds[i].SetActive(i < activeLEDs);
            leds[i].GetComponent<MeshRenderer>().material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
        }

        yield break; // 단발성 동작이므로 즉시 종료
    }

    void DeActivateLighting(List<GameObject> leds)
    {
        for (int i = 0; i < leds.Count; i++)
        {
            leds[i].GetComponent<MeshRenderer>().material.color = Color.black;
        }
    }
}