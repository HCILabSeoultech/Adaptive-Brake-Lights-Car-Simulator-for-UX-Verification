using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrakeSystem : MonoBehaviour
{
    public static BrakeSystem instance;
    public LEDController ledController;
    private ILightBehavior standard, frequency, brightness, area;
    public float brakeIntensity = 0f; // 브레이크 강도 값 (0~1)
    
    [Header("Properties")]
    public float frequencyValue;

    private void Awake()
    {
        if(instance == null) instance = this;
    }

    void Start()
    {
        standard = new StandardBrakeLight();
        frequency = new FrequencyBrakeLight();
        brightness = new BrightnessBrakeLight();
        area = new AreaBrakeLight();
    }

    void Update()
    {
        // 키보드 입력으로 브레이크 강도 조절 (0~1 범위 유지)
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
        {
            brakeIntensity += Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
        {
            brakeIntensity -= Time.deltaTime;
        }

        brakeIntensity = Mathf.Clamp(brakeIntensity, 0f, 1f); // 🚗 0~1 범위로 제한

        // 🚗 키보드 입력으로 제동등 모드 변경
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ledController.SetLightBehavior(standard);
            ledController.ApplyBrakeLight(brakeIntensity);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ledController.SetLightBehavior(frequency);
            ledController.ApplyBrakeLight(brakeIntensity);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ledController.SetLightBehavior(brightness);
            ledController.ApplyBrakeLight(brakeIntensity);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ledController.SetLightBehavior(area);
            ledController.ApplyBrakeLight(brakeIntensity);
        }
    }
}