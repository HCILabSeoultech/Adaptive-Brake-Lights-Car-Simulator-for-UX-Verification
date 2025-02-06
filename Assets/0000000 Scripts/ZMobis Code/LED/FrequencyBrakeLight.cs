using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrequencyBrakeLight : ILightBehavior
{
    private const float FIXED_FREQUENCY = 0.2f; // 🔥 고정 주파수 (Hz) - 1초에 0.2번 깜빡임 (5초 주기)

    public IEnumerator ApplyLighting(List<GameObject> leds, float intensity)
    {
        float blinkInterval = 1 / BrakeSystem.instance.frequencyValue; //FIXED_FREQUENCY; // 🔥 5초마다 한 번씩 깜빡이도록 설정
        Debug.Log(1/ blinkInterval + " 초 주기로 blink");
        while (true) // 새로운 상태가 설정되면 LEDController에서 종료됨
        {
            foreach (var led in leds)
            {
                RevertColor(led); // 현재 상태 반전 (ON/OFF)
            }
            yield return new WaitForSeconds(BrakeSystem.instance.frequencyValue); // 🔥 5초 대기 후 반복
        }
    }

    public void SetColor(List<GameObject> leds, float intensity)
    {
        Color lightColor = intensity > 0.1 ? Color.red : Color.black;

        foreach (var led in leds)
        {
            led.GetComponent<MeshRenderer>().material.color = lightColor;
        }   
    }

    public void RevertColor(GameObject led)
    {
        Material material = led.GetComponent<MeshRenderer>().material;
        Color color = material.color;
        led.GetComponent<MeshRenderer>().material.color = color == Color.black ? Color.red : Color.black;
    }
}