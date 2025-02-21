using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.XInput;
using static PlayerCarController;


public class PlayerCarController : MonoBehaviour
{
    // 아두이노 쓰기위한 코드
    public SerialController serialController;
    LogitechGSDK.LogiControllerPropertiesData properties;

    // For tutorial, see the Data section below and Start().
    private GameObject Take_over;

    internal enum driver
    {
        Logiwheel,
        keyboard_Player,
        automatic,
        None
    }

    [SerializeField] driver driveContreller;

    // 로지텍 관련 키 인풋
    public float rawSteeringInput;
    public float rawForwardInput;

    public float parkInput = 0;
    public float totalTorque; // The total torque requested by the user, will be split between the four wheels
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

    private float steeringReduction; // Used to make it easier to drive with keyboard in higher speeds
    private const float MAX_BRAKE_TORQUE = 6000; // [Nm] 초기값 8000
    private bool brakeLightIsOn = false;
    Action<bool> doorIsOpenR1LAction; // Described more in Start()

    #endregion

    public GameObject Timer;
    public GameObject otherCar;
    public Rigidbody rb;
    
    public enum DrivingMode {Autonomous, BrakeControl}
    public DrivingMode driveMode;
    private void Start()
    {
        parkInput = 0;
        StartCoroutine(TestRoutine());

    }
    public float targetSpeed_KmPerHour; // 목표 속도 (km/h)
    public float targetAcceleration; // 목표 가속도 (m/s²)
    public float durationSpeedUp; // 목표 가속 시간 (s)
    public const float durationSpeedDown = 3f; // 목표 시간 (s)

    private Coroutine currentCoroutine; // 현재 실행 중인 코루틴 저장

    public IEnumerator TestRoutine()
    {
        SetDriveMode(DrivingMode.Autonomous);
        float targetSpeedMS = CarUtils.ConvertKmHToMS(targetSpeed_KmPerHour);
        yield return AccelerateToTargetSpeed(targetSpeedMS, durationSpeedUp);
        yield return StartCoroutine(WaitAtTargetSpeed(5));
        yield return StartCoroutine(WaitAtTargetSpeedUntilBrake());
        SetDriveMode(DrivingMode.BrakeControl);
    }

    public void SetDriveMode(DrivingMode mode)
    {
        driveMode = mode;
    }
    
    /// <summary>
    /// 목표 속도와 목표 시간이 주어지면, Lerp를 활용하여 등가속도 운동을 수행합니다.
    /// </summary>
    public IEnumerator AccelerateToTargetSpeed(float targetSpeed, float duration)
    {
        float elapsedTime = 0f;
        Vector3 initialVelocity = rb.velocity; // 초기 속도 저장
        Vector3 targetVelocity = new Vector3(0, 0, targetSpeed);
        float calculatedAcceleration = (targetSpeed - initialVelocity.z) / duration;
    
        float previousVelocityZ = initialVelocity.z; // 이전 속도 저장
        float measuredAcceleration = 0f; // 실제 측정된 가속도

        Debug.Log($"🚀 목표 속도 설정: {targetSpeed} m/s | 목표 시간: {duration}s | 계산된 가속도: {calculatedAcceleration}");
        int count = 0;
        List<float> accelerations = new List<float>();
        while (elapsedTime < duration)
        {
            float t = Mathf.Clamp01(elapsedTime / duration); // 0~1 보간 비율 유지
            rb.velocity = Vector3.Lerp(initialVelocity, targetVelocity, t);

            // 실제 측정된 가속도 계산 (Δv / Δt)
            measuredAcceleration = (rb.velocity.z - previousVelocityZ) / Time.deltaTime;
            previousVelocityZ = rb.velocity.z; // 현재 속도를 이전 속도로 저장

            // Debug.Log($"⏳ 시간: {elapsedTime:F2}/{duration}s | 속도: {rb.velocity.z:F3} m/s | 목표 속도: {targetSpeed} m/s | 측정 가속도: {measuredAcceleration:F3} m/s²");

            elapsedTime += Time.deltaTime;
            count++;
            accelerations.Add(measuredAcceleration);
            yield return null; // 다음 프레임까지 대기
        }

        float averageAcceleration = accelerations.Sum() / accelerations.Count;
        rb.velocity = targetVelocity; // 최종 속도 보정
        Debug.Log($"✅ 목표 속도 도달: {rb.velocity.z} m/s, 계산된 가속도: {calculatedAcceleration}, 평균 가속도 : {averageAcceleration}, 가속도 오차: {Math.Abs(calculatedAcceleration-averageAcceleration)/calculatedAcceleration* 100:F2}% ");
    }
    /// <summary>
    /// 현재 속도를 유지한 채 일정 시간 동안 대기합니다.
    /// </summary>
    public IEnumerator WaitAtTargetSpeed(float waitTime)
    {
        float elapsedTime = 0f;
        Vector3 constantVelocity = rb.velocity; // 현재 속도 저장

        Debug.Log($"⏳ {waitTime}s 동안 속도 유지: {constantVelocity.z:F3} m/s");

        while (elapsedTime < waitTime)
        {
            rb.velocity = constantVelocity; // 속도 유지
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"✅ {waitTime}s 대기 완료. 속도 유지 후 다음 동작 진행.");
    }
    /// <summary>
    /// 현재 속도를 유지한 채 브레이크 입력값이 들어올 때 까지 속도를 유지합니다.
    /// </summary>
    public IEnumerator WaitAtTargetSpeedUntilBrake()
    {
        Vector3 constantVelocity = rb.velocity; // 현재 속도 저장

        Debug.Log($"현재 속도를 유지한 채 브레이크 입력값이 들어올 때 까지 속도를 유지합니다. {constantVelocity.z:F3} m/s");

        if (driveContreller == driver.keyboard_Player)
        {
            while (Input.GetAxis("Jump") <= 0)
            {
                rb.velocity = constantVelocity; // 속도 유지
                yield return null;
            }
        }
        else if (driveContreller == driver.Logiwheel)
        {
            while (LogitechInput.GetAxis("Brake Vertical") <= 0)
            {
                rb.velocity = constantVelocity; // 속도 유지
                yield return null;
            }    
        }
        
        Debug.Log($"실험자 브레이크 밟음. 속도 유지 로직 탈출.");
    }
    private void FixedUpdate()
    {
        if (driveMode == DrivingMode.BrakeControl)
        {
            switch (driveContreller)
            {
                case driver.Logiwheel:
                    GetLogitechInput();
                    break;
                case driver.keyboard_Player:
                    GetKeyboardInput();
                    break;
                case driver.automatic:
                    AutomaticDrive();
                    break;
            }

            if (LogitechGSDK.LogiUpdate() && LogitechGSDK.LogiIsConnected((int)LogitechKeyCode.FirstIndex))
            {
                //������ �ǵ�� ����
                LogitechGSDK.LogiPlaySpringForce(0, 0, 50, 50); //핸들포스 중앙으로!!
                MoveWheelTorques();
            }
            else
            {
                // Editor 
                MoveWheelTorques();
            }
            
        }
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

        if (driveMode == DrivingMode.BrakeControl)
        {
            if (parkInput > 0)
            {
                // 사용자가 브레이크를 밟았을 경우 (Hand Brake)
                if (Mathf.Abs(velocity.Value) > 5f / 3.6f)
                {
                    totalTorque = -MAX_BRAKE_TORQUE; // 일반 제동
                }
                else
                {
                    totalTorque = -9000; // P모드 제동
                    propulsiveDirection.Value = 0;
                    gearLeverIndication.Value = 0;
                }
                //Debug.Log($"🛑 Brake Applied - totalTorque: {totalTorque}");
            }
            else
            {
                // 🚗 브레이크를 밟지 않은 경우, 새로운 토크 계산식 적용
                totalTorque = Mathf.Min(
                    availableForwardTorque.Evaluate(Mathf.Abs(velocity.Value)),
                    0 + 7900 * rawForwardInput - 9500 * rawForwardInput * rawForwardInput +
                    9200 * rawForwardInput * rawForwardInput * rawForwardInput
                );

                //Debug.Log($"🚗 BrakeControl Mode - totalTorque: {totalTorque}");
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

    private void GetLogitechInput()
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

    /// <summary>
    /// W(Vertical): 전진, A, B(Horizontal): 핸들 좌우, Space bar(Jump): 브레이크
    /// </summary>
    private void GetKeyboardInput()
    {
        rawSteeringInput = Input.GetAxis("Horizontal");
        rawForwardInput = Input.GetAxis("Vertical");
        parkInput = Input.GetAxis("Jump");
    }

    private void AutomaticDrive()
    {
        
    }
    
    /// <summary>
    /// 기존 제어 코드
    /// </summary>
    public void MoveWheelTorquesOrigin()
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
                Debug.Log($"totalTorque : {totalTorque}");
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
                    -500 + 7900 * rawForwardInput - 9500 * rawForwardInput * rawForwardInput +
                    9200 * rawForwardInput * rawForwardInput * rawForwardInput);
                Debug.Log($"totalTorque : {totalTorque}, availableForwardTorque.Evaluate(Mathf.Abs(velocity.Value): {availableForwardTorque.Evaluate(Mathf.Abs(velocity.Value))}, B: {-1800 + 7900 * rawForwardInput - 9500 * rawForwardInput * rawForwardInput + 9200 * rawForwardInput * rawForwardInput * rawForwardInput}");
            }
            else
            {
                totalTorque = -Mathf.Abs(rawForwardInput) * MAX_BRAKE_TORQUE;
                if (Mathf.Abs(velocity.Value) < 0.01f && brakeToReverse)
                {
                    propulsiveDirection.Value = -1;
                    gearLeverIndication.Value = 1;
                }
                Debug.Log($"totalTorque : {totalTorque}");
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
                Debug.Log($"totalTorque : {totalTorque}");
            }
            else
            {
                totalTorque = -Mathf.Abs(rawForwardInput) * MAX_BRAKE_TORQUE;
                if (Mathf.Abs(velocity.Value) < 0.01f)
                {
                    propulsiveDirection.Value = 1;
                    gearLeverIndication.Value = 3;
                }
                Debug.Log($"totalTorque : {totalTorque}");
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
                Debug.Log($"totalTorque : {totalTorque}");
            }
            else if (gearLeverIndication.Value == 0)
            {
                totalTorque = -9000;
                Debug.Log($"totalTorque : {totalTorque}");
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
}