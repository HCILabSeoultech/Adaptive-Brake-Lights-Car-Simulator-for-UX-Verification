using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem.XInput;
using static VehiclesAheadController;


public class VehiclesAheadController : MonoBehaviour
{
    LogitechGSDK.LogiControllerPropertiesData properties;

    // For tutorial, see the Data section below and Start().
    private GameObject Take_over;

    // 로지텍 관련 키 인풋
    public float rawSteeringInput;
    public float rawForwardInput;
    [HideInInspector] public float parkInput = 0;
    public float backwardgear; // 후진

    [Header("주행 Test")]
    /// <summary>
    /// The total torque requested by the user, will be split between the four wheels
    /// </summary>
    public float totalTorque;

    public float currentAcceleration;
    [Space] public float currentVelocity;
    public float currentTime;
    [Space] public float targetSpeedFirst;
    public float targetTimeFirst;


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

        StartCoroutine(VehiclesAheadRoutine());
    }

    public IEnumerator VehiclesAheadRoutine()
    {
        Debug.Log("루틴 1");
        StartCoroutine(AdjustSpeedOverTime(targetSpeedFirst, targetTimeFirst)); // 5초 동안 60km/h로 가속
        // StartCoroutine(AdjustSameAccelerationOverTime(targetSpeedFirst, targetTimeFirst)); // 5초 동안 60km/h로 가속
        // Debug.Log("루틴 2");
        yield return new WaitForSeconds(5f); // 5초 유지
        // Debug.Log("루틴 3");
        // StartCoroutine(AdjustSpeedOverTime(40, 3f)); // 3초 동안 40km/h로 감귀
    }

    private float lastSpeed = 0f; // 이전 프레임의 속도
    private float lastTime = 0f; // 이전 프레임의 시간

    private void FixedUpdate()
    {
        float currentSpeed = 3.6f * Mathf.Abs(velocity.Value) + 0.9f; // 현재 속도를 km/h 단위로 변환
        float currentVelocityMS = Mathf.Abs(velocity.Value); // 속도를 m/s 단위로 유지
        currentVelocity = currentSpeed;
        
        // 가속도 a = (v - v0) / (t - t0)  (m/s 단위 사용)
        float currentTime = Time.time; // 현재 시간
        float acceleration = (currentVelocityMS - lastSpeed) / (currentTime - lastTime);
        currentAcceleration = acceleration;
        
        Debug.Log($"| 가속도: {acceleration:F3} m/s² | rawForwardInput: {rawForwardInput:F3} | 속도: {currentSpeed:F2} km/h | ");

        // 현재 속도를 다음 프레임을 위한 기준 값으로 저장
        lastSpeed = currentVelocityMS; // m/s 단위로 저장
        lastTime = currentTime;

        /*LogitechGSDK.DIJOYSTATE2ENGINES rec;
        rec = LogitechGSDK.LogiGetStateUnity(0);*/

        // If Enter is pressed, toggle the value of doorIsOpenR1L (toggle the state of the front left door).
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            doorIsOpenR1L.Value = !doorIsOpenR1L.Value;
        }

        // low 값에 따라 토크 적용
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

    public IEnumerator AdjustSpeedOverTime(float targetSpeed, float duration)
    {
        float startTime = Time.time;
        float endTime = startTime + duration;

        while (Time.time < endTime)
        {
            float elapsedTime = Time.time - startTime;
            float timeRemaining = endTime - Time.time; // 남은 시간 계산

            // 현재 속도 업데이트
            float currentSpeed = 3.6f * Mathf.Abs(velocity.Value) + 0.9f;
            float speedDifference = targetSpeed + 1 - currentSpeed;

            // 🔹 남은 시간 보정값 추가 (남은 시간이 적을수록 속도를 더 빠르게 보정)
            float timeFactor = Mathf.Clamp01(timeRemaining / duration); // 0 ~ 1 사이 값 유지
            float adjustmentFactor = Mathf.Lerp(50f, 100f, 1 - timeFactor); // 점점 더 강하게 보정

            // 🔹 속도 차이를 고려한 `rawForwardInput` 조정 (가속이 필요할 때 더 강하게)
            rawForwardInput = Mathf.Clamp((speedDifference / (targetSpeed + 1)) * adjustmentFactor, 0f, 1f);

            // Debug.Log($"현재 속도: {currentSpeed} km/h | 목표 속도: {targetSpeed} km/h | rawForwardInput: {rawForwardInput} | 남은 시간: {timeRemaining:F2}s");

            yield return null;
        }

        // 도달 후 정속 주행 유지
        rawForwardInput = 0.5f; // 정속 주행 유지 (필요시 수정 가능)
        Debug.Log($"속도 증가 완료. 최종 속도: {3.6f * Mathf.Abs(velocity.Value) + 0.9f} km/h, 목표 속도: {targetSpeed} km/h");
    }

    public IEnumerator AdjustSameAccelerationOverTime(float targetSpeed, float duration)
    {
        float startTime = Time.time;
        float startSpeed = 3.6f * Mathf.Abs(velocity.Value) + 0.9f; // 현재 속도 변환
        float acceleration = (targetSpeed - startSpeed) / duration; // 등가속도 계산

        bool isAccelerating = targetSpeed > startSpeed; // 🚗 가속 여부 판별

        while (Time.time - startTime < duration)
        {
            float elapsedTime = Time.time - startTime;

            // 등가속도 공식 적용: v = v0 + at
            float expectedSpeed = startSpeed + acceleration * elapsedTime;
            float currentSpeed = 3.6f * Mathf.Abs(velocity.Value) + 0.9f;

            // 가속/감속에 따라 rawForwardInput 조정
            rawForwardInput = Mathf.Clamp((expectedSpeed - currentSpeed) / targetSpeed, isAccelerating ? 0f : -1f,
                isAccelerating ? 1f : 0f);

            Debug.Log(
                $"현재 속도: {currentSpeed:F2} km/h | 목표 속도: {targetSpeed} km/h | rawForwardInput: {rawForwardInput:F3} | 남은 시간: {duration - elapsedTime:F2}s");

            // 디버깅
            currentVelocity = currentSpeed;
            currentTime = elapsedTime;

            yield return null;
        }

        // 목표 속도 도달 후 정속 주행 유지
        rawForwardInput = isAccelerating ? 0.5f : 0f; // 감속 후 0, 가속 후 정속 주행 유지
        Debug.Log($"속도 조정 완료. 최종 속도: {3.6f * Mathf.Abs(velocity.Value) + 0.9f} km/h, 목표 속도: {targetSpeed} km/h");
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
                // 🔹 속도 증가 시 토크 감소를 방지하기 위해 보정 계수 추가
                float speedFactor = 1.0f - (Mathf.Abs(velocity.Value) / 100f);
                speedFactor = Mathf.Clamp(speedFactor, 0.5f, 1f); // 최소 50% 보장

                // 🔹 기존 totalTorque 연산에 speedFactor 적용하여 일정한 토크 유지
                totalTorque = Mathf.Min(
                    availableForwardTorque.Evaluate(Mathf.Abs(velocity.Value)) / speedFactor, // 보정된 최대 토크
                    (-1800
                     + 7900 * rawForwardInput
                     - 9500 * rawForwardInput * rawForwardInput
                     + 9200 * rawForwardInput * rawForwardInput * rawForwardInput) * speedFactor
                );
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


    private void AutomaticDrive()
    {
    }
}