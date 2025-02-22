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
    }

    void Start()
    {
        standard = new StandardBrakeLight();
        frequency = new FrequencyBrakeLight();
        brightness = new BrightnessBrakeLight();
        area = new AreaBrakeLight();
    }
    
    public void ActiveStandardBrakeLight()
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(standard);
        LEDController.instance.ApplyBrakeLight(BrakeSystem.instance.brakeIntensity, DrivingScenarioManager.Instance.durationSpeedDown);
    }

    public void ActiveFrequencyBrakeLight()
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(frequency);
        LEDController.instance.ApplyBrakeLight(BrakeSystem.instance.brakeIntensity, DrivingScenarioManager.Instance.durationSpeedDown);
    }

    public void ActiveBrightnessBrakeLight()
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(brightness);
        LEDController.instance.ApplyBrakeLight(BrakeSystem.instance.brakeIntensity, DrivingScenarioManager.Instance.durationSpeedDown);
    }

    public void ActiveAreaBrakeLight()
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(area);
        LEDController.instance.ApplyBrakeLight(BrakeSystem.instance.brakeIntensity, DrivingScenarioManager.Instance.durationSpeedDown);
    }
}
