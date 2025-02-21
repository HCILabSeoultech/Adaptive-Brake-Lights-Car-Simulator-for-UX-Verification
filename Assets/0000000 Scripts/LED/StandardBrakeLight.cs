using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StandardBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers, float intensity, float duration)
    {
        Color lightColor = intensity > 0 ? Color.red : Color.black;

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