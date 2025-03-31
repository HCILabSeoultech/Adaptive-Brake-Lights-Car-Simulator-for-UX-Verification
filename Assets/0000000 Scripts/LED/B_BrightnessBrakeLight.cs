using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class B_BrightnessBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers,
        float acceleration, float duration)
    {
        mainBrakeRenderer.material.color = Color.red;
        Color lightColor = Color.black;
        if (DrivingScenarioManager.Instance.level == Level.수준2)
        {
            if (acceleration >= -4f)
            {
                lightColor = new Color(0.5f, 0, 0);
                // 180
            }
            else
            {
                lightColor = new Color(1f, 0, 0); 
                // 255
            }
        }
        else if (DrivingScenarioManager.Instance.level == Level.수준3)
        {
            if (acceleration >= -3f)
            {
                lightColor = new Color(0.4f, 0, 0);
                // 150
            }
            else if (acceleration >= -5f)
            {
                lightColor = new Color(0.6f, 0, 0);
                // 190
            }
            else
            {
                lightColor = new Color(1f, 0, 0);
                // 255
            }
        }

        // Color lightColor = Color.Lerp(Color.black, Color.red, acceleration);
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