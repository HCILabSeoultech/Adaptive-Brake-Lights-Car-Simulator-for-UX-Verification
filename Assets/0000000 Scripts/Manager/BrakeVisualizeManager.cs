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
