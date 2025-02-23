using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class B_BrightnessBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers,
        float acceleration, float duration)
    {
        mainBrakeRenderer.material.color = Color.red;

        if (DrivingScenarioManager.Instance.level == Level.level2)
        {
            if (acceleration >= -4f)
            {
                acceleration = 0.5f;
            }
            else
            {
                acceleration = 1f;
            }
        }
        else if (DrivingScenarioManager.Instance.level == Level.level3)
        {
            if (acceleration >= -2f)
            {
                acceleration = 0.4f;
            }
            else if (acceleration >= -5f)
            {
                acceleration = 0.7f;
            }
            else
            {
                acceleration = 1f;
            }
        }

        Color lightColor = Color.Lerp(Color.black, Color.red, acceleration);
        foreach (var led in subBrakeRenderers)
        {
            led.material.color = lightColor;
        }

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