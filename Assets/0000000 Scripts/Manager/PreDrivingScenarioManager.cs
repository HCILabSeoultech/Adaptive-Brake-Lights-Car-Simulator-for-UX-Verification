using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

// 선두 차량과 실험자 차량 상호작용을 해야 함. 선두 차량의 상태와 실험자 차량의 상태를 중앙 제어하는 시스템
public class PreDrivingScenarioManager : MonoBehaviour
{
    public static PreDrivingScenarioManager Instance;
    public Level level;
    public int count = 0;
    List<(int, float)> scenarios = new List<(int, float)>();
    public int _currentBrakePatternIndex;
    [Header("Driving Condition")] public float startConditionSpeed_KmPerHour = 100f;
    public float durationSpeedDown = 2.5f;
    public float startConditionDistance = 20f;

    [Tooltip("시작 조건(속도, 거리) 완료 후 다음 시나리오 시작까지의 대기 시간")]
    public float startWaitingTime = 5f;
    public float reasonableDistance = 5;

    [Header("Car Conrtoller")] public OtherCarController otherCarController;
    public PlayerCarController playerCarController;
    private Coroutine otherCarCoroutine_MaintainTargetSpeed;
    private Coroutine playerCarCoroutine_MaintainTargetSpeed;
    [Header("DEBUG")] public TextMeshProUGUI descriptionText;
    [Header("Driving scenario settings")] public BrakePatternType[] brakePatternTypes;
    
    // public float[] accelerations;
    // public float[] distances;
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        StartCoroutine(RoutineExperiment());
    }

    #region 루틴 별 호출

    private IEnumerator RoutineExperiment()
    {
        AudioManager.Instance.PlayStartDrivingAudio();
        yield return StartCoroutine(RoutineByBrakePatternTypes(BrakePatternType.기본제동등A));
        AudioManager.Instance.PlayEndDrivingAudio();
        PrintResults();
    }

    private IEnumerator RoutineByBrakePatternTypes(BrakePatternType brakePatternType)
    {
        switch (brakePatternType)
        {
            case BrakePatternType.기본제동등A:
                yield return StartCoroutine(Routine_A_StandardBrakeLight());
                break;
            case BrakePatternType.밝기변화제동등B:
                yield return StartCoroutine(Routine_B_BrightnessBrakeLight());
                break;
            case BrakePatternType.점멸주파수변화제동등C:
                yield return StartCoroutine(Routine_C_FrequencyBrakeLight());
                break;
            case BrakePatternType.면적변화제동등D:
                yield return StartCoroutine(Routine_D_AreaBrakeLight());
                break;
            default:
                break;
        }
    }
    private IEnumerator Routine_A_StandardBrakeLight()
    {
        /*List<(float, float)> combinedList = new List<(float, float)>();
        for (int i = 0; i < distances.Length; i++)
        {
            combinedList.Add((accelerations[i], distances[i]));
        }
        
        Debug.Log($"Starting Scenario {level} A_StandardBrakeLight");
        yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(combinedList[0].Item2));
        yield return StartCoroutine(WaitForScenarioStart());*/
        
        List<(float, float)> shuffledList = CarUtils.GetRandomDecelerationDistanceList(level);

        Debug.Log("Starting Scenario D_StandardBrakeLight");
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(shuffledList[0].Item2));
        yield return StartCoroutine(WaitForScenarioStart());
        
        // 랜덤한 순서로 호출
        for (int i = 0; i < shuffledList.Count; i++)
        {
            yield return StartCoroutine(ExecuteScenarioRoutine(BrakePatternType.기본제동등A, shuffledList[i]));
            if (i == shuffledList.Count - 1)
            {
                yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(20));
            }
            else
            {
                yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(shuffledList[i + 1].Item2));
            }

            yield return StartCoroutine(WaitForScenarioStart(Random.Range(3.0f, 5.0f)));
        }
    }

    private IEnumerator Routine_B_BrightnessBrakeLight()
    {
        List<(float, float)> shuffledList = CarUtils.GetRandomDecelerationDistanceList(level);

        Debug.Log("Starting Scenario B_StandardBrakeLight");
        yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(shuffledList[0].Item2));
        yield return StartCoroutine(WaitForScenarioStart());
        // 랜덤한 순서로 호출
        for (int i = 0; i < shuffledList.Count; i++)
        {
            UserDataLoggingManager.Instance.SetCanWrite(true);
            yield return StartCoroutine(
                ExecuteScenarioRoutine(BrakePatternType.밝기변화제동등B, shuffledList[i]));
            if (i == shuffledList.Count - 1)
            {
                yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(20));
            }
            else
            {
                yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(shuffledList[i + 1].Item2));
            }

            yield return StartCoroutine(WaitForScenarioStart(Random.Range(3.0f, 5.0f)));
        }
    }

    private IEnumerator Routine_C_FrequencyBrakeLight()
    {
        List<(float, float)> shuffledList = CarUtils.GetRandomDecelerationDistanceList(level);

        Debug.Log("Starting Scenario C_StandardBrakeLight");
        yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(shuffledList[0].Item2));
        yield return StartCoroutine(WaitForScenarioStart());

        // 랜덤한 순서로 호출
        for (int i = 0; i < shuffledList.Count; i++)
        {
            UserDataLoggingManager.Instance.SetCanWrite(true);
            yield return StartCoroutine(ExecuteScenarioRoutine(BrakePatternType.점멸주파수변화제동등C, shuffledList[i]));
            if (i == shuffledList.Count - 1)
            {
                yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(20));
            }
            else
            {
                yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(shuffledList[i + 1].Item2));
            }

            yield return StartCoroutine(WaitForScenarioStart(Random.Range(3.0f, 5.0f)));
        }
    }

    private IEnumerator Routine_D_AreaBrakeLight()
    {
        List<(float, float)> shuffledList = CarUtils.GetRandomDecelerationDistanceList(level);

        Debug.Log("Starting Scenario D_StandardBrakeLight");
        yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(shuffledList[0].Item2));
        yield return StartCoroutine(WaitForScenarioStart());

        // 랜덤한 순서로 호출
        for (int i = 0; i < shuffledList.Count; i++)
        {
            UserDataLoggingManager.Instance.SetCanWrite(true);
            yield return StartCoroutine(ExecuteScenarioRoutine(BrakePatternType.면적변화제동등D, shuffledList[i]));
            if (i == shuffledList.Count - 1)
            {
                yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(20));
            }
            else
            {
                yield return StartCoroutine(AlignVehiclesBy100KmHAndTargetDistance(shuffledList[i + 1].Item2));
            }

            yield return StartCoroutine(WaitForScenarioStart(Random.Range(3.0f, 5.0f)));
        }
    }

    #endregion

    #region 시나리오 실제 제어

    public IEnumerator ExecuteScenarioRoutine(BrakePatternType brakePatternType, (float, float) accelerationAndDistance)
    { 
        descriptionText.text = $"수준: {level}, \n 감속률: {accelerationAndDistance.Item1}m/s^2, \n 간격: {accelerationAndDistance.Item2}m, \n 제동등: {brakePatternType}";
        Debug.Log($"시나리오 호출 : {level}, {brakePatternType}, {accelerationAndDistance.Item1}m/s^2으로 감속");

        StartCoroutine(CheckPreTestValue());
        
        startConditionDistance = accelerationAndDistance.Item2;
        StartCoroutine(playerCarController.SetCanDriveState()); 
        yield return StartCoroutine(
            otherCarController.ExecuteBehaviourByScenario(brakePatternType, accelerationAndDistance.Item1));

        Debug.Log("시나리오 종료합니다.");
    }

    public IEnumerator CheckPreTestValue()
    {
        float startTime = Time.time;
        while (Time.time - startTime  < durationSpeedDown) // durationSpeedDown초 동안 지속 
        {
            float current = GetCurrentDistance();
            
            // 현재 count에 이미 값이 있다면 최소값 비교
            int index = scenarios.FindIndex(x => x.Item1 == count); // ✅ count로 검색

            if (index >= 0)
            {
                if (current < scenarios[index].Item2)
                {
                    // 더 작은 값으로 갱신
                    scenarios[index] = (count, current);
                }
            }
            else
            {
                // 해당 count에 대한 값이 없으면 처음 추가
                scenarios.Add((count, current));
            }
            yield return null;
        }

        count++;
    }

    void PrintResults()
    {
        foreach ((int count, float distance) in scenarios)
        {
            string result = distance < 10f ? "[Fail]" : "[Success]";
            Debug.Log($"Count: {count}, MinDistance: {distance}, {result}");
        }
    }

    public IEnumerator AlignVehiclesBy100KmHAndTargetDistance(float targetDistance)
    {
        AudioManager.Instance.PlayRearrangementAudio();
        
        Debug.Log($"선두 차량, 실험자 차량 정렬 시도 | 목표 속도: {startConditionSpeed_KmPerHour}km/h, 목표 간격: {targetDistance}");
        playerCarController.SetDriveMode(PlayerCarController.DrivingMode.Autonomous);

        // 선두 차량 100km/h 정렬
        float targetSpeedMS = CarUtils.ConvertKmHToMS(startConditionSpeed_KmPerHour);
        StartCoroutine(playerCarController.AccelerateToTargetSpeed(targetSpeedMS - 2, 5));
        yield return StartCoroutine(otherCarController.AccelerateToTargetSpeed(targetSpeedMS, 5));
        otherCarCoroutine_MaintainTargetSpeed = StartCoroutine(otherCarController.MaintainSpeed());

        // 후방 차량 {startConditionSpeed_KmPerHour}km/h, 간격 {startConditionDistance}m 정렬
        yield return StartCoroutine(
            playerCarController.AlignTestCarToSpeedAndGap(targetSpeedMS, targetDistance, 10));
        playerCarCoroutine_MaintainTargetSpeed = StartCoroutine(playerCarController.MaintainSpeed());

        yield return StartCoroutine(WaitForScenarioReady(targetDistance));
        Debug.Log(
            $"선두 차량, 실험자 차량 정렬 완료 | 목표 속도: {startConditionSpeed_KmPerHour}km/h, 목표 간격: {startConditionDistance}");
    }

    public IEnumerator WaitForScenarioStart(float randomTime = 0)
    {
        // 기존의 속도 유지 로직 로직 정지
        if (otherCarCoroutine_MaintainTargetSpeed != null) StopCoroutine(otherCarCoroutine_MaintainTargetSpeed);
        if (playerCarCoroutine_MaintainTargetSpeed != null) StopCoroutine(playerCarCoroutine_MaintainTargetSpeed);

        Debug.Log($"선두 차량, 실험자 차량 {startWaitingTime}초 동안 속도 유지");
        otherCarCoroutine_MaintainTargetSpeed =
            StartCoroutine(otherCarController.MaintainSpeedForWaitTime(startWaitingTime));
        playerCarCoroutine_MaintainTargetSpeed =
            StartCoroutine(playerCarController.MaintainSpeedForWaitTime(startWaitingTime));
        if (randomTime == 0f)
        {
            Debug.Log($"일정 시간 대기 ({startWaitingTime}s)");
            yield return new WaitForSeconds(startWaitingTime);
        }
        else
        {
            Debug.Log($"랜덤 시간 대기 ({randomTime}s)");
            yield return new WaitForSeconds(randomTime);
        }

        // 속도 유지 로직 정지
        if (otherCarCoroutine_MaintainTargetSpeed != null) StopCoroutine(otherCarCoroutine_MaintainTargetSpeed);
        if (playerCarCoroutine_MaintainTargetSpeed != null) StopCoroutine(playerCarCoroutine_MaintainTargetSpeed);
        Debug.Log($"선두 차량, 실험자 차량 속도 유지 종료");
    }


    public IEnumerator WaitForScenarioReady(float targetDistance)
    {
        // IsScenarioReady()가 true를 반환할 때까지 매 프레임마다 대기합니다.
        int failCount = 0;
        while (!IsScenarioReady(targetDistance))
        {
            failCount++;
            if (failCount >= 50)
            {
                Debug.Log("시다리오 시작 조건 누적 실패, 거리 재조정 시도");
                float targetSpeedMS = CarUtils.ConvertKmHToMS(startConditionSpeed_KmPerHour);
                yield return StartCoroutine(
                    playerCarController.AlignTestCarToSpeedAndGap(targetSpeedMS, targetDistance, 5));
                break;
            }

            yield return null;
        }

        // true가 되면 여기서 실행이 계속됩니다.
        Debug.Log("시나리오 시작 조건이 준비되었습니다.");
    }

    public bool IsScenarioReady(float targetDistance)
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
            GetCurrentDistance();
        bool distanceAligned = Mathf.Abs(currentDistance - targetDistance) <= toleranceDistance;
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

    public bool IsConflictWithOtherCar()
    {
        if (GetCurrentDistance() < 4.5f)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool IsReasonableDistance()
    {
        float maxDistance = startConditionDistance + reasonableDistance;
        float minDistance = startConditionDistance - reasonableDistance;

        float currentDistance = GetCurrentDistance();

        if (currentDistance < maxDistance && currentDistance > minDistance)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    #endregion
}