using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// 선두 차량과 실험자 차량 상호작용을 해야 함. 선두 차량의 상태와 실험자 차량의 상태를 중앙 제어하는 시스템
public class DrivingScenarioManager : MonoBehaviour
{
    public static DrivingScenarioManager Instance;

    [Header("Driving scenario settings")] 
    public BrakePatternType[] brakePatternTypes;
    public Level level;
    private int _currentBrakePatternIndex;
    [Header("Driving Condition")] 
    public float startConditionSpeed_KmPerHour = 100f;
    public float startConditionDistance = 20f;
    public float durationSpeedDown = 2.5f;
    [Tooltip("시작 조건(속도, 거리) 완료 후 다음 시나리오 시작까지의 대기 시간")]
    public float startWaitingTime = 5f;

    [Header("Car Conrtoller")] public OtherCarController otherCarController;
    public PlayerCarController playerCarController;
    private Coroutine otherCarCoroutine_MaintainTargetSpeed;
    private Coroutine playerCarCoroutine_MaintainTargetSpeed;
    [Header("DEBUG")]
    public TextMeshProUGUI descriptionText;

    private void Awake()
    {
        if (!Instance) Instance = this;
    }

    private void Start()
    {
        StartCoroutine(RoutineExperiment());
    }

    #region 루틴 별 호출

    private IEnumerator RoutineExperiment()
    {
        yield return StartCoroutine(RoutineByBrakePatternTypes(brakePatternTypes[0]));
        yield return StartCoroutine(RoutineByBrakePatternTypes(brakePatternTypes[1]));
        yield return StartCoroutine(RoutineByBrakePatternTypes(brakePatternTypes[2]));
        yield return StartCoroutine(RoutineByBrakePatternTypes(brakePatternTypes[3]));
    } 
    
    private IEnumerator RoutineByBrakePatternTypes(BrakePatternType brakePatternType)
    {
        switch (brakePatternType)
        {
            case BrakePatternType.A_StandardBrakeLight:
                yield return StartCoroutine(Routine_A_StandardBrakeLight());
                break;
            case BrakePatternType.B_BrightnessBrakeLight:
                yield return StartCoroutine(Routine_B_BrightnessBrakeLight());
                break;
            case BrakePatternType.C_FrequencyBrakeLight:
                yield return StartCoroutine(Routine_C_FrequencyBrakeLight());
                break;
            case BrakePatternType.D_AreaBrakeLight:
                yield return StartCoroutine(Routine_D_AreaBrakeLight());
                break;
            default:
                break;
        }
    }
    private IEnumerator Routine_A_StandardBrakeLight()
    {
        Debug.Log($"Starting Scenario {level} A_StandardBrakeLight");
        yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
        yield return StartCoroutine(WaitForScenarioStart());

        List<float> shuffledList = CarUtils.GetRandomizedAccelerationsOrder();
        // 랜덤한 순서로 호출
        for (int i = 0; i < shuffledList.Count; i++)
        {
            yield return StartCoroutine(ExecuteScenarioRoutine(BrakePatternType.A_StandardBrakeLight, shuffledList[i]));
            yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
            yield return StartCoroutine(WaitForScenarioStart());
        }
    }

    private IEnumerator Routine_B_BrightnessBrakeLight()
    {
        Debug.Log("Starting Scenario A_StandardBrakeLight");
        yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
        yield return StartCoroutine(WaitForScenarioStart());

        List<float> shuffledList = CarUtils.GetRandomizedAccelerationsOrder();
        // 랜덤한 순서로 호출
        for (int i = 0; i < shuffledList.Count; i++)
        {
            yield return StartCoroutine(
                ExecuteScenarioRoutine(BrakePatternType.B_BrightnessBrakeLight, shuffledList[i]));
            yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
            yield return StartCoroutine(WaitForScenarioStart());
        }
    }

    private IEnumerator Routine_C_FrequencyBrakeLight()
    {
        Debug.Log("Starting Scenario A_StandardBrakeLight");
        yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
        yield return StartCoroutine(WaitForScenarioStart());

        List<float> shuffledList = CarUtils.GetRandomizedAccelerationsOrder();
        // 랜덤한 순서로 호출
        for (int i = 0; i < shuffledList.Count; i++)
        {
            yield return StartCoroutine(ExecuteScenarioRoutine(BrakePatternType.C_FrequencyBrakeLight, shuffledList[i]));
            yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
            yield return StartCoroutine(WaitForScenarioStart());
        }
    }

    private IEnumerator Routine_D_AreaBrakeLight()
    {
        Debug.Log("Starting Scenario A_StandardBrakeLight");
        yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
        yield return StartCoroutine(WaitForScenarioStart());

        List<float> shuffledList = CarUtils.GetRandomizedAccelerationsOrder();
        // 랜덤한 순서로 호출
        for (int i = 0; i < shuffledList.Count; i++)
        {
            yield return StartCoroutine(ExecuteScenarioRoutine(BrakePatternType.D_AreaBrakeLight, shuffledList[i]));
            yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
            yield return StartCoroutine(WaitForScenarioStart());
        }
    }

    #endregion

    #region 시나리오 실제 제어

    public IEnumerator ExecuteScenarioRoutine(BrakePatternType brakePatternType, float acceleration)
    {
        descriptionText.text = $"{level}, {acceleration}m/s^2, Brake: {brakePatternType}";
        Debug.Log($"시나리오 호출 : {level}, {brakePatternType}, {acceleration}m/s^2으로 감속, 해당 시나리오가 끝날 때까지 대기합니다.");

        StartCoroutine(playerCarController.SetCanDriveState());
        yield return StartCoroutine(otherCarController.ExecuteBehaviourByScenario(brakePatternType, acceleration));

        Debug.Log($"시나리오 호출 : {level}, {brakePatternType}, {acceleration}m/s^2으로 감속, 해당 시나리오를 종료합니다.");
    }

    public IEnumerator AlignVehiclesBySpeedAndDistance()
    {
        Debug.Log($"선두 차량, 실험자 차량 정렬 시도 | 목표 속도: {startConditionSpeed_KmPerHour}km/h, 목표 간격: {startConditionDistance}");
        playerCarController.SetDriveMode(PlayerCarController.DrivingMode.Autonomous);
        
        // 선두 차량 100km/h 정렬
        float targetSpeedMS = CarUtils.ConvertKmHToMS(startConditionSpeed_KmPerHour);
        StartCoroutine(playerCarController.AccelerateToTargetSpeed(targetSpeedMS - 2, 5));
        yield return StartCoroutine(otherCarController.AccelerateToTargetSpeed(targetSpeedMS, 5));
        otherCarCoroutine_MaintainTargetSpeed = StartCoroutine(otherCarController.MaintainSpeed());
        
        // 후방 차량 100km/h, 간격 20m 정렬
        yield return StartCoroutine(playerCarController.AlignTestCarToSpeedAndGap(targetSpeedMS, 20, 10));
        playerCarCoroutine_MaintainTargetSpeed = StartCoroutine(playerCarController.MaintainSpeed());

        yield return StartCoroutine(WaitForScenarioReady());
        Debug.Log($"선두 차량, 실험자 차량 정렬 완료 | 목표 속도: {startConditionSpeed_KmPerHour}km/h, 목표 간격: {startConditionDistance}");
    }

    public IEnumerator WaitForScenarioStart()
    {
        // 기존의 속도 유지 로직 로직 정지
        if (otherCarCoroutine_MaintainTargetSpeed != null) StopCoroutine(otherCarCoroutine_MaintainTargetSpeed);
        if (playerCarCoroutine_MaintainTargetSpeed != null) StopCoroutine(playerCarCoroutine_MaintainTargetSpeed);

        Debug.Log($"선두 차량, 실험자 차량 {startWaitingTime}초 동안 속도 유지");
        otherCarCoroutine_MaintainTargetSpeed =
            StartCoroutine(otherCarController.MaintainSpeedForWaitTime(startWaitingTime));
        playerCarCoroutine_MaintainTargetSpeed =
            StartCoroutine(playerCarController.MaintainSpeedForWaitTime(startWaitingTime));
        yield return new WaitForSeconds(startWaitingTime);

        // 속도 유지 로직 정지
        if (otherCarCoroutine_MaintainTargetSpeed != null) StopCoroutine(otherCarCoroutine_MaintainTargetSpeed);
        if (playerCarCoroutine_MaintainTargetSpeed != null) StopCoroutine(playerCarCoroutine_MaintainTargetSpeed);
        Debug.Log($"선두 차량, 실험자 차량 속도 유지 종료");
    }


    public IEnumerator WaitForScenarioReady()
    {
        // IsScenarioReady()가 true를 반환할 때까지 매 프레임마다 대기합니다.
        while (!IsScenarioReady())
        {
            yield return null;
        }

        // true가 되면 여기서 실행이 계속됩니다.
        Debug.Log("시나리오 시작 조건이 준비되었습니다.");
    }

    public bool IsScenarioReady()
    {
        float toleranceSpeed = 1f; // km/h 단위 허용 오차
        float toleranceDistance = 0.5f; // m 단위 허용 오차

        // 실험자 차량과 선두 차량의 속도 (m/s를 km/h로 변환: 1 m/s = 3.6 km/h)
        float playerSpeed = playerCarController.rb.velocity.magnitude * 3.6f;
        float otherSpeed = otherCarController.rb.velocity.magnitude * 3.6f;

        bool speedAligned = (Mathf.Abs(playerSpeed - startConditionSpeed_KmPerHour) <= toleranceSpeed) &&
                            (Mathf.Abs(otherSpeed - startConditionSpeed_KmPerHour) <= toleranceSpeed);

        // 두 차량 사이의 거리 (m 단위)
        float currentDistance =
            Vector3.Distance(playerCarController.transform.position, otherCarController.transform.position);
        bool distanceAligned = Mathf.Abs(currentDistance - startConditionDistance) <= toleranceDistance;
        Debug.Log($"속도 조건: {speedAligned}, 간격 조건: {distanceAligned}");
        return speedAligned && distanceAligned;
    }

    public void SetCurrentScenarioIndex(int scenarioIndex)
    {
        _currentBrakePatternIndex = scenarioIndex;
    }

    public float GetCurrentDistance()
    {
        return Vector3.Distance(otherCarController.transform.position, playerCarController.transform.position);
    }

    #endregion
}

public enum Level
{
    level2,
    level3
}

public enum BrakePatternType
{
    A_StandardBrakeLight,
    B_BrightnessBrakeLight,
    C_FrequencyBrakeLight,
    D_AreaBrakeLight,
}