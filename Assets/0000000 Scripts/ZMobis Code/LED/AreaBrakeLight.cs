using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaBrakeLight : ILightBehavior
{
    public IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers, float intensity)
    {
        
        int activeLEDs = Mathf.RoundToInt(1 * subBrakeRenderers.Count);
        DeActivateLighting(subBrakeRenderers, mainBrakeRenderer);
        mainBrakeRenderer.material.color = Color.red;
        
        for (int i = 0; i < subBrakeRenderers.Count; i++)
        {
            subBrakeRenderers[i].gameObject.SetActive(i < activeLEDs);
            subBrakeRenderers[i].material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
        }
        
        yield break; // 단발성 동작이므로 즉시 종료
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