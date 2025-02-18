using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrightnessBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers, float intensity)
    {
        mainBrakeRenderer.material.color = Color.red;
        
        if (intensity < 0.3)
        {
            intensity = 0.4f;
        }else if (intensity < 0.6)
        {
            intensity = 0.7f;
        }
        else
        {
            intensity = 1f;
        }
        
        Color lightColor = Color.Lerp(Color.black, Color.red, intensity);
        foreach (var led in subBrakeRenderers)
        {
            led.material.color = lightColor;
        }
        yield break; // 단발성 동작이므로 즉시 종료
    }

}