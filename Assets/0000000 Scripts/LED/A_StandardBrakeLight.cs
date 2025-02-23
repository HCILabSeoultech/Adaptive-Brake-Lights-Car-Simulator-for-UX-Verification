using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class A_StandardBrakeLight : ILightBehavior
{
    // 감속률에 따른 변화 없음. 켜졌다. 꺼졌다. (코드 유지)
    public IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers, float acceleration, float duration)
    {
        Color lightColor = acceleration > 0 ? Color.red : Color.black;

        foreach (var led in subBrakeRenderers)
        {
            led.material.color = lightColor;
        }
        mainBrakeRenderer.material.color = lightColor;

        yield return new WaitForSeconds(duration);
        DeActivateLighting(subBrakeRenderers, mainBrakeRenderer);
    }
    void DeActivateLighting(List<MeshRenderer> leds, MeshRenderer mainBrakeRenderer)
    {
        for (int i = 0; i < leds.Count; i++)
        {
            leds[i].material.color = Color.black;
        }
        mainBrakeRenderer.material.color = Color.black;
    }
}