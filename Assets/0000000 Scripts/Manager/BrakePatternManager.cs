using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrakePatternManager : MonoBehaviour
{
    public Material mainBrakeDefaultMaterial;
    public Material subBrakeDefaultMaterial;
    public Material mainBrakeActivatedMaterial;
    public Material subBrakeActivatedMaterial;
 
    public static BrakePatternManager instance;
    private ILightBehavior standard, frequency, brightness, area;
    private void Awake()
    {
        if (instance == null) instance = this;
        standard = new A_StandardBrakeLight();
        brightness = new B_BrightnessBrakeLight();
        frequency = new C_FrequencyBrakeLight();
        area = new D_AreaBrakeLight();
    }
    
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
