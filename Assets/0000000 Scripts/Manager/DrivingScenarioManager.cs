using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 선두 차량과 실험자 차량 상호작용을 해야 함. 선두 차량의 상태와 실험자 차량의 상태를 중앙 제어하는 시스템
public class DrivingScenarioManager : MonoBehaviour
{
    public static DrivingScenarioManager Instance;
    
    [Header("Driving scenario settings")]
    public ScenarioType[] scenarioTypes;
    public Level level;
    private int _currentScenarioIndex;
    [Header("Driving Condition")]
    public float startConditionSpeed_KmPerHour = 100f;
    public float startConditionDistance = 20f;
    
    [Header("Car Conrtoller")]
    public OtherCarController otherCarController;
    public PlayerCarController playerCarController;
    
    private void Awake()
    {
        if(!Instance) Instance = this;
        StartCoroutine(TestRoutine());
    }
    
    private IEnumerator TestRoutine()
    {
        Debug.Log("Starting Scenario 1");
        yield return StartCoroutine(AlignVehiclesBySpeedAndDistance());
        yield return StartCoroutine(ExecuteScenarioRoutine());
        
        yield return null;
    }

    public IEnumerator AlignVehiclesBySpeedAndDistance()
    {
        Debug.Log($"선두 차량, 실험자 차량 정렬 시도 | 목표 속도: {startConditionSpeed_KmPerHour}km/h, 목표 간격: {startConditionDistance}");
        
        float targetSpeedMS = CarUtils.ConvertKmHToMS(startConditionSpeed_KmPerHour);
        yield return StartCoroutine(otherCarController.AccelerateToTargetSpeed(targetSpeedMS, 5));
        Coroutine otherCarCoroutine_MainyainTargetSpeed = StartCoroutine(otherCarController.MaintainSpeed());
        yield return StartCoroutine(playerCarController.AlignTestCarToSpeedAndGap(targetSpeedMS, 20, 10));
        Coroutine playerCarCoroutine_MainyainTargetSpeed = StartCoroutine(playerCarController.MaintainSpeed());
        
        yield return StartCoroutine(WaitForScenarioReady());
        Debug.Log($"선두 차량, 실험자 차량 정렬 완료 | 목표 속도: {startConditionSpeed_KmPerHour}km/h, 목표 간격: {startConditionDistance}");
    }

    public IEnumerator ExecuteScenarioRoutine()
    {
        
        yield return null;
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
        float toleranceSpeed = 1f;     // km/h 단위 허용 오차
        float toleranceDistance = 0.5f;  // m 단위 허용 오차

        // 실험자 차량과 선두 차량의 속도 (m/s를 km/h로 변환: 1 m/s = 3.6 km/h)
        float playerSpeed = playerCarController.rb.velocity.magnitude * 3.6f;
        float otherSpeed = otherCarController.rb.velocity.magnitude * 3.6f;

        bool speedAligned = (Mathf.Abs(playerSpeed - startConditionSpeed_KmPerHour) <= toleranceSpeed) &&
                            (Mathf.Abs(otherSpeed - startConditionSpeed_KmPerHour) <= toleranceSpeed);

        // 두 차량 사이의 거리 (m 단위)
        float currentDistance = Vector3.Distance(playerCarController.transform.position, otherCarController.transform.position);
        bool distanceAligned = Mathf.Abs(currentDistance - startConditionDistance) <= toleranceDistance;
        Debug.Log($"speedAligned: {speedAligned}, distanceAligned: {distanceAligned}");
        return speedAligned && distanceAligned;
    }
    
    public void SetCurrentScenarioIndex(int scenarioIndex)
    {
        _currentScenarioIndex = scenarioIndex;
    }

    public float GetCurrentDistance()
    {
        return Vector3.Distance(otherCarController.transform.position, playerCarController.transform.position);
    }
}

public enum Level
{
    level2,
    level3
}
public enum ScenarioType
{
    A_StandardBrakeLight,
    B_BrightnessBrakeLight,
    C_FrequencyBrakeLight,
    D_AreaBrakeLight,
}
