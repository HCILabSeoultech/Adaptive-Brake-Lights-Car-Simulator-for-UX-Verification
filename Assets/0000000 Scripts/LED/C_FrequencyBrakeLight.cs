using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class C_FrequencyBrakeLight : ILightBehavior
{
    private const float FIXED_FREQUENCY = 0.2f; // 🔥 고정 주파수 (Hz) - 1초에 0.2번 깜빡임 (5초 주기)

    private float highFrequencyValue = 0.1f;
    private float midFrequencyValue = 0.2f;
    private float lowFrequencyValue = 0.3f;

    public IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers,
        float acceleration, float duration)
    {
        mainBrakeRenderer.material.color = Color.red;


        float blinkInterval=0; // = 1 / BrakeSystem.instance.frequencyValue;

        if (DrivingScenarioManager.Instance.level == Level.level2)
        {
            if (acceleration >= -4f)
            {
                blinkInterval = lowFrequencyValue;
            }
            else
            {
                blinkInterval = highFrequencyValue;
            }
        }
        else if (DrivingScenarioManager.Instance.level == Level.level3)
        {
            if (acceleration >= -2f)
            {
                blinkInterval = lowFrequencyValue;
            }
            else if (acceleration >= -5f)
            {
                blinkInterval = midFrequencyValue;
            }
            else
            {
                blinkInterval = highFrequencyValue;
            }
        }
        Debug.Log(blinkInterval + " 초 주기로 blink");
        float time = 0;
        while (time < duration) // 새로운 상태가 설정되면 LEDController에서 종료됨
        {
            time += blinkInterval;
            foreach (var led in subBrakeRenderers)
            {
                RevertColor(led); // 현재 상태 반전 (ON/OFF)
            }

            yield return new WaitForSeconds(blinkInterval); //BrakeSystem.instance.frequencyValue);
        }

        DeActivateLighting(subBrakeRenderers, mainBrakeRenderer);
    }

    public void SetColor(List<GameObject> leds, float intensity)
    {
        Color lightColor = intensity > 0.1 ? Color.red : Color.black;

        foreach (var led in leds)
        {
            led.GetComponent<MeshRenderer>().material.color = lightColor;
        }
    }

    public void RevertColor(MeshRenderer led)
    {
        Material material = led.material;
        Color color = material.color;
        led.material.color = color == Color.black ? Color.red : Color.black;
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