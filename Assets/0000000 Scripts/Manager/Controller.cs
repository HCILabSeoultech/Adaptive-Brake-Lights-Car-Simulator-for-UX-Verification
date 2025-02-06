using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem.XInput;
using static Controller;


public class Controller : MonoBehaviour
{
    // 아두이노 쓰기위한 코드
    public SerialController serialController;
    LogitechGSDK.LogiControllerPropertiesData properties;

    // For tutorial, see the Data section below and Start().
    private GameObject Take_over;

    internal enum driver
    {
        Logiwheel,
        moblie
    }

    [SerializeField] driver driveContreller;

    // 로지텍 관련 키 인풋
    public float rawSteeringInput;
    public float rawForwardInput;

    [HideInInspector] public float parkInput = 0;
    public float backwardgear; // 후진

    [Header("Information")] [SerializeField]
    private InfoText info = new InfoText(
        "This demo controller lets you control the car using the axes named Horizontal, Vertical and Jump. " +
        "If you are using keyboard and standard Unity settings, this means either arrow keys or WASD together with Space.");

    [Header("Settings")] [SerializeField] private bool brakeToReverse = true;

    [SerializeField] private InfoText infoAboutCurves =
        new InfoText(
            "The curves below describe available total wheel torque (Nm, y axis) vs vehicle speed (m/s, x axis).");

    [SerializeField] private AnimationCurve availableForwardTorque = AnimationCurve.Constant(0, 50, 2700);
    [SerializeField] private AnimationCurve availableReverseTorque = AnimationCurve.Linear(0, 2700, 15, 0);

    [SerializeField] [Tooltip("Print tutorial messages to console?")]
    private bool consoleMessages = true;

    [Header("Data")] // This is how you reference custom data, both for read and write purposes.
    [SerializeField]
    private VolvoCars.Data.PropulsiveDirection propulsiveDirection = default;

    [SerializeField] private VolvoCars.Data.WheelTorque wheelTorque = default;
    [SerializeField] private VolvoCars.Data.UserSteeringInput userSteeringInput = default;
    [SerializeField] private VolvoCars.Data.Velocity velocity = default;
    [SerializeField] private VolvoCars.Data.GearLeverIndication gearLeverIndication = default;
    [SerializeField] private VolvoCars.Data.DoorIsOpenR1L doorIsOpenR1L = default; // R1L stands for Row 1 Left.
    [SerializeField] private VolvoCars.Data.LampBrake lampBrake = default;

    #region Private variables not shown in the inspector

    private VolvoCars.Data.Value.Public.WheelTorque
        wheelTorqueValue =
            new VolvoCars.Data.Value.Public.WheelTorque(); // This is the value type used by the wheelTorque data item.     

    private VolvoCars.Data.Value.Public.LampGeneral
        lampValue = new VolvoCars.Data.Value.Public.LampGeneral(); // This is the value type used by lights/lamps

    private float totalTorque; // The total torque requested by the user, will be split between the four wheels
    private float steeringReduction; // Used to make it easier to drive with keyboard in higher speeds
    public const float MAX_BRAKE_TORQUE = 6000; // [Nm] 초기값 8000
    private bool brakeLightIsOn = false;
    Action<bool> doorIsOpenR1LAction; // Described more in Start()

    #endregion


    //자율주행 모드를 위한 트랙생성 및 자율주행모드 거리 및 핸들조절
    public trackWaypoints waypoints;
    public Transform currentWaypoint;
    public List<Transform> nodes = new List<Transform>();
    public int distanceOffset = 1;
    public float sterrForce = 1.54f;
    public string status = "off"; // 주행모드 선택 디폴트 off


    public GameObject Timer;

    private void Start()
    {
        // Subscribe to data items this way. (There is also a SubscribeImmediate method if you don't need to be on the main thread / game loop.)
        // First define the action, i.e. what should happen when an updated value comes in:
        doorIsOpenR1LAction = isOpen =>
        {
            if (consoleMessages && Application.isPlaying)
                Debug.Log(
                    "This debug message is an example action triggered by a subscription to DoorIsOpenR1L in DemoCarController. Value: " +
                    isOpen +
                    "\nYou can turn off this message by unchecking Console Messages in the inspector.");
        };
        // Then, add it to the subscription. In this script's OnDestroy() method we are also referencing this action when unsubscribing.
        doorIsOpenR1L.Subscribe(doorIsOpenR1LAction);

        // How to publish, example:
        // doorIsOpenR1L.Value = true;

        parkInput = 0;
    }

    private void FixedUpdate()
    {
        /*LogitechGSDK.DIJOYSTATE2ENGINES rec;
        rec = LogitechGSDK.LogiGetStateUnity(0);*/

        // If Enter is pressed, toggle the value of doorIsOpenR1L (toggle the state of the front left door).
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            doorIsOpenR1L.Value = !doorIsOpenR1L.Value;
        }

        // Driving inputs 
        /* LogitechGSDK.DIJOYSTATE2ENGINES rec;
         rec = LogitechGSDK.LogiGetStateUnity(0);*/
        switch (driveContreller)
        {
            case driver.Logiwheel:
                LogitechDrive();
                break;
            case driver.moblie:
                mobileDrive();
                break;
        }

        if (LogitechGSDK.LogiUpdate() && LogitechGSDK.LogiIsConnected((int)LogitechKeyCode.FirstIndex))
        {
            //������ �ǵ�� ����
            LogitechGSDK.LogiPlaySpringForce(0, 0, 50, 50); //핸들포스 중앙으로!!

            #region

            //float rawSteeringInput = LogitechInput.GetAxis("Steering Horizontal");
            //float rawForwardInput = LogitechInput.GetAxis("Gas Vertical");
            //float parkInput = LogitechInput.GetAxis("Brake Vertical");

            #endregion

            MoveWheelTorques();
        }
        else
        {
            // Editor 
            MoveWheelTorques();
        }
    }

    private void OnDestroy()
    {
        doorIsOpenR1L.Unsubscribe(doorIsOpenR1LAction);
    }

    private void ApplyWheelTorques(float totalWheelTorque)
    {
        // Set the torque values for the four wheels.
        wheelTorqueValue.fL = 1.4f * totalWheelTorque / 4f;
        wheelTorqueValue.fR = 1.4f * totalWheelTorque / 4f;
        wheelTorqueValue.rL = 0.6f * totalWheelTorque / 4f;
        wheelTorqueValue.rR = 0.6f * totalWheelTorque / 4f;

        // Update the wheel torque data item with the new values. This is accessible to other scripts, such as chassis dynamics.
        wheelTorque.Value = wheelTorqueValue;
    }

    public void MoveWheelTorques()
    {
        steeringReduction = 1 - Mathf.Min(Mathf.Abs(velocity.Value) / 30f, 0.85f);
        userSteeringInput.Value = rawSteeringInput * steeringReduction;

        #region Wheel torques

        if (parkInput > 0)
        {
            // Park request ("hand brake")
            if (Mathf.Abs(velocity.Value) > 5f / 3.6f)
            {
                totalTorque = -MAX_BRAKE_TORQUE; // Regular brakes
            }
            else
            {
                totalTorque = -9000; // Parking brake and/or gear P
                propulsiveDirection.Value = 0;
                gearLeverIndication.Value = 0;
            }
        }
        else if (propulsiveDirection.Value == 1)
        {
            // Forward

            if (rawForwardInput >= 0 && velocity.Value > -1.5f)
            {
                totalTorque = Mathf.Min(availableForwardTorque.Evaluate(Mathf.Abs(velocity.Value)),
                    -1800 + 7900 * rawForwardInput - 9500 * rawForwardInput * rawForwardInput +
                    9200 * rawForwardInput * rawForwardInput * rawForwardInput);
            }
            else
            {
                totalTorque = -Mathf.Abs(rawForwardInput) * MAX_BRAKE_TORQUE;
                if (Mathf.Abs(velocity.Value) < 0.01f && brakeToReverse)
                {
                    propulsiveDirection.Value = -1;
                    gearLeverIndication.Value = 1;
                }
            }
        }
        else if (propulsiveDirection.Value == -1)
        {
            // Reverse
            if (rawForwardInput <= 0 && velocity.Value < 1.5f)
            {
                float absInput = Mathf.Abs(rawForwardInput);
                totalTorque = Mathf.Min(availableReverseTorque.Evaluate(Mathf.Abs(velocity.Value)),
                    -1800 + 7900 * absInput - 9500 * absInput * absInput + 9200 * absInput * absInput * absInput);
            }
            else
            {
                totalTorque = -Mathf.Abs(rawForwardInput) * MAX_BRAKE_TORQUE;
                if (Mathf.Abs(velocity.Value) < 0.01f)
                {
                    propulsiveDirection.Value = 1;
                    gearLeverIndication.Value = 3;
                }
            }
        }
        else
        {
            // No direction (such as neutral gear or P)
            totalTorque = 0;
            if (Mathf.Abs(velocity.Value) < 1f)
            {
                if (rawForwardInput > 0)
                {
                    propulsiveDirection.Value = 1;
                    gearLeverIndication.Value = 3;
                }
                else if (rawForwardInput < 0 && brakeToReverse)
                {
                    propulsiveDirection.Value = -1;
                    gearLeverIndication.Value = 1;
                }
            }
            else if (gearLeverIndication.Value == 0)
            {
                totalTorque = -9000;
            }
        }

        ApplyWheelTorques(totalTorque);

        #endregion

        #region Lights

        bool userBraking = (rawForwardInput < 0 && propulsiveDirection.Value == 1) ||
                           (rawForwardInput > 0 && propulsiveDirection.Value == -1);
        if (userBraking && !brakeLightIsOn)
        {
            lampValue.r = 1;
            lampValue.g = 0;
            lampValue.b = 0;
            lampValue.intensity = 1;
            lampBrake.Value = lampValue;
            brakeLightIsOn = true;
        }
        else if (!userBraking && brakeLightIsOn)
        {
            lampValue.intensity = 0;
            lampBrake.Value = lampValue;
            brakeLightIsOn = false;
        }

        #endregion
    }

    private void LogitechDrive()
    {
        rawSteeringInput = LogitechInput.GetAxis("Steering Horizontal");
        rawForwardInput = LogitechInput.GetAxis("Gas Vertical");
        parkInput = LogitechInput.GetAxis("Brake Vertical");
        // SteeringInput = LogitechInput.GetAxis("Steering Horizontal");
        backwardgear = LogitechInput.GetAxis("Clutch Vertical");

        if (backwardgear > 0)
        {
            rawForwardInput = -1 * rawForwardInput;
        }
        else
        {
            rawForwardInput = LogitechInput.GetAxis("Gas Vertical");
        }
    }

    private void mobileDrive()
    {
        rawSteeringInput = Input.GetAxis("Horizontal");
        rawForwardInput = Input.GetAxis("Vertical");
        parkInput = Input.GetAxis("Jump");
    }
}