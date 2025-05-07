using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class OtherCarController : MonoBehaviour
{
    public Rigidbody rb; // Rigidbody 참조
    public float targetAccelderation; // 목표 가속도 (m/s²)
    private Coroutine currentCoroutine; // 현재 실행 중인 코루틴 저장
    public GameObject playerCar;

    public IEnumerator ExecuteBehaviourByScenario(BrakeLightType brakeLightType, float acceleration)
    {
        targetAccelderation = acceleration;
        switch (brakeLightType)
        {
            case BrakeLightType.기본제동등A:
                BrakeVisualizeManager.instance.ActiveStandardBrakeLight(acceleration);
                break;
            case BrakeLightType.밝기변화제동등B:
                BrakeVisualizeManager.instance.ActiveBrightnessBrakeLight(acceleration);
                break;
            case BrakeLightType.점멸주파수변화제동등C:
                BrakeVisualizeManager.instance.ActiveFrequencyBrakeLight(acceleration);
                break;
            case BrakeLightType.면적변화제동등D:
                BrakeVisualizeManager.instance.ActiveAreaBrakeLight(acceleration);
                break;
            default:
                break;
        }

        yield return StartCoroutine(AccelerateWithFixedAcceleration(acceleration,
            DrivingScenarioManager.Instance != null
                ? DrivingScenarioManager.Instance.durationSpeedDown
                : PreDrivingScenarioManager.Instance.durationSpeedDown));
        yield return StartCoroutine(MaintainSpeedForWaitTime(2));
        targetAccelderation = 0;
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
        Debug.Log(
            $"✅ 목표 속도 도달: {rb.velocity.z} m/s, 계산된 가속도: {calculatedAcceleration}, 평균 가속도 : {averageAcceleration}, 가속도 오차: {Math.Abs(calculatedAcceleration - averageAcceleration) / calculatedAcceleration * 100:F2}% ");
    }

    /// <summary>
    /// B 차량(실험 차량)을 S-curve 기반으로 목표 속도 및 목표 간격에 맞춰 정렬하는 코루틴
    /// </summary>
    /// <param name="targetSpeed">목표 속도 (m/s)</param>
    /// <param name="targetGap">목표 간격 (m)</param>
    /// <param name="transitionTime">가속 및 감속을 수행할 시간 (s)</param>
    /// <returns>코루틴 실행</returns>
    public IEnumerator AlignTestCarToSpeedAndGap(float targetSpeed, float targetGap, float transitionTime)
    {
        float startTime = Time.time;
        float speed_A = 27.78f; // A 차량 속도 (100 km/h)
        float position_A0 = GetOtherCarPosZ();

        float speed_B0 = rb.velocity.z; // B 차량 초기 속도
        float position_B0 = transform.position.z; // B 차량 초기 위치

        // ✅ [수정 1] A 차량이 B보다 targetGap만큼 뒤에 위치해야 하므로, 초기 간격을 반대로 계산
        float targetGap_0 = GetPlayerCarPosZ() - GetOtherCarPosZ();

        float elapsedTime = 0f;

        Debug.Log($"차량 정렬 시작! 초기 속도: {speed_B0:F2} m/s, 목표 속도: {targetSpeed} m/s, 목표 간격: 앞차가 {targetGap}m 뒤에 위치해야 함");

        while (elapsedTime < transitionTime)
        {
            elapsedTime += Time.deltaTime;

            // A 차량 등속 위치
            float currentPosition_A = position_A0 + speed_A * elapsedTime;

            // S-curve 기반 속도 계산
            float t = elapsedTime / transitionTime;

            float currentSpeed_B = CalculateSpeed(Time.time, startTime, startTime + transitionTime, speed_B0,
                targetSpeed, targetGap_0, -targetGap); // ✅ [수정 2] 목표 간격을 음수로 전달

            // B 차량 위치 업데이트
            float currentPosition_B = position_B0 + (speed_B0 * elapsedTime) +
                                      (0.5f * (currentSpeed_B - speed_B0) * elapsedTime);

            // 현재 간격 계산
            float currentGap = 0;
            if (DrivingScenarioManager.Instance != null)
            {
                currentGap = DrivingScenarioManager.Instance.playerCarController.transform.position.z
                             - DrivingScenarioManager.Instance.otherCarController.transform.position.z;
            }
            else if (PreDrivingScenarioManager.Instance != null)
            {
                currentGap = PreDrivingScenarioManager.Instance.playerCarController.transform.position.z
                             - PreDrivingScenarioManager.Instance.otherCarController.transform.position.z;
            }

            // 속도 적용
            rb.velocity = new Vector3(0, 0, currentSpeed_B);

            yield return null;
        }

        rb.velocity = new Vector3(0, 0, targetSpeed);
        Debug.Log(
            $"✅ B 차량 정렬 완료! 최종 속도: {rb.velocity.z:F2} m/s, 최종 간격: {GetPlayerCarPosZ() - GetOtherCarPosZ()}m");
    }

    public float GetPlayerCarPosZ()
    {
        if (DrivingScenarioManager.Instance != null)
        {
            return DrivingScenarioManager.Instance.playerCarController.transform.position.z;
        }
        else if (PreDrivingScenarioManager.Instance != null)
        {
            return PreDrivingScenarioManager.Instance.playerCarController.transform.position.z;
        }
        return -1;
    }
    public float GetOtherCarPosZ()
    {
        if (DrivingScenarioManager.Instance != null)
        {
            return DrivingScenarioManager.Instance.otherCarController.transform.position.z;
        }
        else if (PreDrivingScenarioManager.Instance != null)
        {
            return PreDrivingScenarioManager.Instance.otherCarController.transform.position.z;
        }
        return -1;
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
    /// 목표 가속도와 목표 시간이 주어지면, Lerp를 활용하여 등가속도 운동을 수행합니다.
    /// </summary>
    public IEnumerator AccelerateWithFixedAcceleration(float targetAcceleration, float duration)
    {
        Debug.Log($"선두 차량 감속 시작, {targetAcceleration} m/s²");
        float elapsedTime = 0f;
        Vector3 initialVelocity = rb.velocity;
        Vector3 targetVelocity = initialVelocity + new Vector3(0, 0, targetAcceleration * duration); // v = v0 + at

        Debug.Log($"목표 가속도 설정: {targetAcceleration} m/s² | 목표 시간: {duration}s | 목표 속도: {targetVelocity}m/s");


        float previousVelocityZ = initialVelocity.z; // 이전 속도 저장
        float measuredAcceleration = 0f; // 실제 측정된 가속도
        int count = 0;
        List<float> accelerations = new List<float>();
        while (elapsedTime < duration)
        {
            float t = Mathf.Clamp01(elapsedTime / duration);
            rb.velocity = Vector3.Lerp(initialVelocity, targetVelocity, t);

            // 실제 측정된 가속도 계산 (Δv / Δt)
            measuredAcceleration = (rb.velocity.z - previousVelocityZ) / Time.deltaTime;
            previousVelocityZ = rb.velocity.z; // 현재 속도를 이전 속도로 저장

            // Debug.Log($"⏳ 시간: {elapsedTime:F2}/{duration}s | 속도: {rb.velocity.z:F3} m/s | 측정 가속도: {measuredAcceleration:F3} m/s² | 목표 가속도: {targetAcceleration} m/s²");

            elapsedTime += Time.deltaTime;
            count++;
            accelerations.Add(measuredAcceleration);
            yield return null;
        }

        float averageAcceleration = accelerations.Sum() / accelerations.Count;
        rb.velocity = targetVelocity;
        Debug.Log(
            $"✅ 목표 가속도 적용 완료. 최종 속도: {rb.velocity.z} m/s, 목표 가속도: {targetAcceleration}, 평균 가속도 : {averageAcceleration}, 가속도 오차: {Math.Abs(targetAcceleration - averageAcceleration) / targetAcceleration * 100:F2}% ");
    }

    /// <summary>
    /// 현재 속도를 유지한 채 일정 시간 동안 대기합니다.
    /// </summary>
    public IEnumerator MaintainSpeedForWaitTime(float waitTime)
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
    public IEnumerator MaintainSpeed()
    {
        Vector3 constantVelocity = rb.velocity; // 현재 속도 저장

        Debug.Log($"현재 속도를 유지한 채 대기. {constantVelocity.z:F3} m/s");
        while (true)
        {
            rb.velocity = constantVelocity; // 속도 유지
            yield return null;
        }

        yield return null;
    }
}