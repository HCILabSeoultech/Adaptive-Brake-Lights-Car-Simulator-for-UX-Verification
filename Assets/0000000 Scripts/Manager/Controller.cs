using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public PlayerCarController.DrivingMode driveMode;

    public void SetDriveMode(PlayerCarController.DrivingMode mode)
    {
        if (driveMode != mode)
        {
            Debug.Log($"플레이어 차량 모드를 {driveMode} -> {mode}로 설정합니다.");
            driveMode = mode;
        }
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

        if (driveMode == PlayerCarController.DrivingMode.BrakeControl)
        {
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
        else
        {
            
        }
    }

    public Rigidbody rb;

    /// <summary>
    /// B 차량(실험 차량)을 S-curve 기반으로 목표 속도 및 목표 간격에 맞춰 정렬하는 코루틴
    /// </summary>
    /// <param name="targetSpeed">목표 속도 (m/s)</param>
    /// <param name="targetGap">목표 간격 (m)</param>
    /// <param name="transitionTime">가속 및 감속을 수행할 시간 (s)</param>
    /// <returns>코루틴 실행</returns>
    public IEnumerator AlignTestCarToSpeedAndGap(float targetSpeed, float targetGap, float transitionTime)
    {
        // TODO: S-Curve 가감속 패턴으로 구현
        float startTime = Time.time;
        float speed_A = 27.78f; // A 차량 속도 (100 km/h)
        float position_A0 = LeadCarStateMachine.Instance.leadCarController.transform.position.z;

        float speed_B0 = rb.velocity.z; // B 차량 초기 속도 (예: 80 km/h)
        float position_B0 = transform.position.z; // B 차량 초기 위치
        float targetGap_0 = LeadCarStateMachine.Instance.leadCarController.transform.position.z - transform.position.z;

        float elapsedTime = 0f;

        Debug.Log($"차량 정렬 시작! 초기 속도: {speed_B0:F2} m/s, 목표 속도: {targetSpeed} m/s, 목표 간격: {targetGap}m 뒤");

        while (elapsedTime < transitionTime)
        {
            elapsedTime += Time.deltaTime;

            // 현재 A 차량의 위치 (등속 운동)
            float currentPosition_A = position_A0 + speed_A * elapsedTime;

            // 🚀 S-curve 기반 속도 변화 (부드러운 가속 및 감속)
            float t = elapsedTime / transitionTime;

            float currentSpeed_B = CalculateSpeed(Time.time, startTime, startTime + transitionTime, speed_B0,
                targetSpeed, targetGap_0, targetGap);
            // 현재 B 차량의 위치 업데이트
            float currentPosition_B = position_B0 + (speed_B0 * elapsedTime) +
                                      (0.5f * (currentSpeed_B - speed_B0) * elapsedTime);

            // 현재 간격 계산
            float currentGap = 0;
            currentGap = LeadCarStateMachine.Instance.leadCarController.transform.position.z
                         - LeadCarStateMachine.Instance.playerCarController.transform.position.z;

            // 🚗 속도 적용
            rb.velocity = new Vector3(0, 0, currentSpeed_B);

            // Debug.Log($"{elapsedTime:F2}/{transitionTime}s | B 속도: {currentSpeed_B:F2} m/s | 현재 간격: {currentGap:F2}m");

            yield return null;
        }

        rb.velocity = new Vector3(0, 0, targetSpeed);
        Debug.Log(
            $"✅ B 차량 정렬 완료! 최종 속도: {rb.velocity.z:F2} m/s, 최종 간격: {LeadCarStateMachine.Instance.playerCarController.transform.position.z - transform.position.z}m");
    }

    /// <summary>
    /// 후행 차량 B의 속도를 계산합니다.
    /// </summary>
    /// <param name="t">현재 시간</param>
    /// <param name="t1">시작 시간 (t1)</param>
    /// <param name="t2">종료 시간 (t2)</param>
    /// <param name="y1">t1에서의 속도 (y1)</param>
    /// <param name="y2">t2에서의 속도 (y2)</param>
    /// <param name="D1">t1에서의 차량 간격 (D1)</param>
    /// <param name="D2">t2에서의 차량 간격 (D2, 예: 20m)</param>
    /// <returns>t 시간에서의 후행 차량 B의 속도</returns>
    public static float CalculateSpeed(float t, float t1, float t2, float y1, float y2, float D1, float D2)
    {
        // t1 ~ t2 사이의 보간 변수 u (0에서 1까지)
        float u = (t - t1) / (t2 - t1);

        // 보정 계수 k 계산 (D1-D2가 음수일 경우에도 올바르게 동작함)
        float k = (Mathf.PI / 2.0f) * ((y2 - y1) / (2.0f * (D1 - D2)) + 1.0f / (t2 - t1));

        // 속도 함수 계산
        float speed = y1 + (y2 - y1) * (1.0f - Mathf.Cos(Mathf.PI * u)) / 2.0f
                         + k * (D1 - D2) * Mathf.Sin(Mathf.PI * u);

        return speed;
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
            accelerations.Add(measuredAcceleration);
            yield return null; // 다음 프레임까지 대기
        }

        float averageAcceleration = accelerations.Sum() / accelerations.Count;
        rb.velocity = targetVelocity; // 최종 속도 보정
        Debug.Log(
            $"✅ 목표 속도 도달: {rb.velocity.z} m/s, 계산된 가속도: {calculatedAcceleration}, 평균 가속도 : {averageAcceleration}, 가속도 오차: {Math.Abs(calculatedAcceleration - averageAcceleration) / calculatedAcceleration * 100:F2}% ");
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
        // Debug.Log(rawSteeringInput + ", " + userSteeringInput.Value);
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