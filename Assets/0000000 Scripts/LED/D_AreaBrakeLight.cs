using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class D_AreaBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers,
        float acceleration, float duration)
    {
        int activeLEDs = Mathf.RoundToInt(1 * subBrakeRenderers.Count);
        DeActivateLighting(subBrakeRenderers, mainBrakeRenderer);
        mainBrakeRenderer.material.color = Color.red;

        // TODO: acceleration(감속률)에 따른 범위 변화로 변경
        if (DrivingScenarioManager.Instance.level == Level.수준2)
        {
            if (acceleration >= -4f)
            {
                for (int i = 0; i < 3; i++)
                {
                    subBrakeRenderers[i].material.color = Color.red;
                }
                Debug.Log("3개 켬");
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    subBrakeRenderers[i].material.color = Color.red;
                }
                Debug.Log("6개 켬");
            }
        }
        else if (DrivingScenarioManager.Instance.level == Level.수준3)
        {
            if (acceleration >= -3f)
            {
                for (int i = 0; i < 2; i++)
                {
                    subBrakeRenderers[i].material.color = Color.red;
                }
                Debug.Log("2개 켬");
            }
            else if (acceleration >= -5f)
            {
                for (int i = 0; i < 4; i++)
                {
                    subBrakeRenderers[i].material.color = Color.red;
                }
                Debug.Log("4개 켬");
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    subBrakeRenderers[i].material.color = Color.red;
                }
                Debug.Log("6개 켬");
            }
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