using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrakeVisualizeManager : MonoBehaviour
{
    public static BrakeVisualizeManager instance;
    private ILightBehavior standard, frequency, brightness, area;
    private void Awake()
    {
        if (instance == null) instance = this;
        standard = new A_StandardBrakeLight();
        brightness = new B_BrightnessBrakeLight();
        frequency = new C_FrequencyBrakeLight();
        area = new D_AreaBrakeLight();
    }

    public void ActiveLight(BrakeLightType lightType, float acceleration, float duration)
    {
        switch (lightType)
        {
            case BrakeLightType.기본제동등A:
                ActiveStandardBrakeLight(acceleration, duration);
                break;
            case BrakeLightType.밝기변화제동등B:
                ActiveBrightnessBrakeLight(acceleration, duration);
                break;
            case BrakeLightType.점멸주파수변화제동등C:
                ActiveFrequencyBrakeLight(acceleration, duration);
                break;
            case BrakeLightType.면적변화제동등D:
                ActiveAreaBrakeLight(acceleration, duration);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lightType), lightType, null);
        }
    }
    // ============================= Exp 2 version =======================================
    public void ActiveStandardBrakeLight(float acceleration, float duration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(standard);
        LEDController.instance.ApplyBrakeLight(acceleration, duration);
    }

    public void ActiveFrequencyBrakeLight(float acceleration, float duration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(frequency);
        LEDController.instance.ApplyBrakeLight(acceleration, duration);
    }

    public void ActiveBrightnessBrakeLight(float acceleration, float duration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(brightness);
        LEDController.instance.ApplyBrakeLight(acceleration, duration);
    }

    public void ActiveAreaBrakeLight(float acceleration, float duration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(area);
        LEDController.instance.ApplyBrakeLight(acceleration, duration);
    }
    
    
    // ============================= Exp 1 version =======================================
    public void ActiveStandardBrakeLight(float acceleration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(standard);
        LEDController.instance.ApplyBrakeLight(acceleration, DrivingScenarioManager.Instance != null ? DrivingScenarioManager.Instance.durationSpeedDown : PreDrivingScenarioManager.Instance.durationSpeedDown);
    }

    public void ActiveFrequencyBrakeLight(float acceleration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(frequency);
        LEDController.instance.ApplyBrakeLight(acceleration, DrivingScenarioManager.Instance.durationSpeedDown);
    }

    public void ActiveBrightnessBrakeLight(float acceleration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(brightness);
        LEDController.instance.ApplyBrakeLight(acceleration, DrivingScenarioManager.Instance.durationSpeedDown);
    }

    public void ActiveAreaBrakeLight(float acceleration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(area);
        LEDController.instance.ApplyBrakeLight(acceleration, DrivingScenarioManager.Instance.durationSpeedDown);
    }
}
