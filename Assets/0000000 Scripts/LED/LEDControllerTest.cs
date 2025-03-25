using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LEDControllerTest : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        // -2, -4, -6
        if(Input.GetKeyDown(KeyCode.Q)) ActiveStandardBrakeLight(-2f);
        else if(Input.GetKeyDown(KeyCode.A)) ActiveStandardBrakeLight(-4f);
        else if(Input.GetKeyDown(KeyCode.Z)) ActiveStandardBrakeLight(-6f);
        else if(Input.GetKeyDown(KeyCode.W)) ActiveFrequencyBrakeLight(-2f);
        else if(Input.GetKeyDown(KeyCode.S)) ActiveFrequencyBrakeLight(-4f);
        else if(Input.GetKeyDown(KeyCode.X)) ActiveFrequencyBrakeLight(-6f);
        else if(Input.GetKeyDown(KeyCode.E)) ActiveBrightnessBrakeLight(-2f);
        else if(Input.GetKeyDown(KeyCode.D)) ActiveBrightnessBrakeLight(-4f);
        else if(Input.GetKeyDown(KeyCode.C)) ActiveBrightnessBrakeLight(-6f);
        else if(Input.GetKeyDown(KeyCode.R)) ActiveAreaBrakeLight(-2f);
        else if(Input.GetKeyDown(KeyCode.F)) ActiveAreaBrakeLight(-4f);
        else if(Input.GetKeyDown(KeyCode.V)) ActiveAreaBrakeLight(-6f);
    }
    private ILightBehavior standard, frequency, brightness, area;
    private void Awake()
    {
        standard = new A_StandardBrakeLight();
        brightness = new B_BrightnessBrakeLight();
        frequency = new C_FrequencyBrakeLight();
        area = new D_AreaBrakeLight();
    }
    public void ActiveStandardBrakeLight(float acceleration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(standard);
        LEDController.instance.ApplyBrakeLight(acceleration, 2.5f);
    }

    public void ActiveFrequencyBrakeLight(float acceleration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(frequency);
        LEDController.instance.ApplyBrakeLight(acceleration, 2.5f);
    }

    public void ActiveBrightnessBrakeLight(float acceleration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(brightness);
        LEDController.instance.ApplyBrakeLight(acceleration, 2.5f);
    }

    public void ActiveAreaBrakeLight(float acceleration)
    {
        LEDController.instance.ResetBrakeLight();
        LEDController.instance.SetLightBehavior(area);
        LEDController.instance.ApplyBrakeLight(acceleration, 2.5f);
    }
}
