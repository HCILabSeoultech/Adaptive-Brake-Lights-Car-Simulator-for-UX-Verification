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
    public IEnumerator ExecuteBehaviourByScenario(BrakePatternType brakePatternType, float acceleration)
    {
        targetAccelderation = acceleration;
        switch (brakePatternType)
        {
            case BrakePatternType.기본제동등:
                BrakePatternManager.instance.ActiveStandardBrakeLight(acceleration);
                break;
            case BrakePatternType.밝기변화제동등:
                BrakePatternManager.instance.ActiveBrightnessBrakeLight(acceleration);
                break;
            case BrakePatternType.점멸주파수변화제동등:
                BrakePatternManager.instance.ActiveFrequencyBrakeLight(acceleration);
                break;
            case BrakePatternType.면적변화제동등:
                BrakePatternManager.instance.ActiveAreaBrakeLight(acceleration);
                break;
            default:
                break;
        }
        yield return StartCoroutine(AccelerateWithFixedAcceleration(acceleration, DrivingScenarioManager.Instance.durationSpeedDown));
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