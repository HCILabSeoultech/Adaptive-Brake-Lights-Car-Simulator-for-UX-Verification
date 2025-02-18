using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LEDController : MonoBehaviour
{
    public static LEDController instance;

    private void Awake()
    {
        if (instance == null) instance = this;
    }
    // 제동등 동작 방식 4가지: 기존 제동등, 주파수, 밝기, 면적 변화

    public MeshRenderer mainBrakeRenderer;
    public List<MeshRenderer> subBrakeRenderers; // LED 역할을 하는 Cube 오브젝트
    private ILightBehavior _currentLightBehavior;
    private Coroutine activeCoroutine; // 현재 실행 중인 코루틴 저장

    public void SetLightBehavior(ILightBehavior newBehavior)
    {
        // 기존 코루틴이 실행 중이면 중지
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        _currentLightBehavior = newBehavior;
    }

    public void ApplyBrakeLight(float intensity)
    {
        if (_currentLightBehavior != null)
        {
            // 기존 코루틴 중지 후 새로운 코루틴 실행
            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            activeCoroutine = StartCoroutine(_currentLightBehavior.ApplyLighting(mainBrakeRenderer, subBrakeRenderers, intensity));
            
        }
    }

    public void ResetBrakeLight()
    {
        // 기존 코루틴이 실행 중이면 중지
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        
        foreach (var led in subBrakeRenderers)
        {
            led.gameObject.SetActive(true);
            led.material.color = Color.black;
        }
        mainBrakeRenderer.material.color = Color.black;
    }
}
