using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 브레이크 패턴(조합)에 따른 선두 차량 제어를 위한 매니저 클래스
/// </summary>
public class BrakePatternManager : MonoBehaviour
{
    public static BrakePatternManager Instance;
    
    [Header("브레이크 패턴(조합)")]
    public BrakePatternBuilder brakePatternBuilder;
    public int[] randomizedOrder;
    public int startPatternIndex = 0;
    [Header("DEBUG")] public TextMeshProUGUI descriptionText;
    
    #region Initialize
    private void Awake()
    {
        Init();
    }
    
    void Init()
    {
        SetInstance();
        SetRandomizedOrder();
        LoadPattern(randomizedOrder[startPatternIndex]);
    }

    void SetInstance()
    {
        if(Instance == null) Instance = this;
    }
    /// <summary>
    /// brakePatterns의 길이에 따라 랜덤화된 순서로 초기화
    /// </summary>
    void SetRandomizedOrder()
    {
        randomizedOrder = Enumerable.Range(0, brakePatternBuilder.brakePatterns.Length).ToArray();
        for (int i = randomizedOrder.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            
            // swap
            (randomizedOrder[i], randomizedOrder[j]) = (randomizedOrder[j], randomizedOrder[i]);
        }

        foreach (int idx in randomizedOrder) Debug.Log(idx);
    }
    #endregion

    private void Start()
    {
        // StartPattern();
    }
    
    #region BrakePattern
    
    private Queue<BrakeStep> stepQueue;
    private Coroutine playCoroutine;

    // Pause/Resume 제어용 플래그
    private bool pauseRequested = false;
    private bool resumeRequested = false;

    public void LoadPattern(int patternIndex)
    {
        BrakePattern brakePattern = brakePatternBuilder.GetPattern(patternIndex);
        if (brakePattern == null || brakePattern.steps == null || brakePattern.steps.Count == 0)
        {
            Debug.LogWarning($"[{patternIndex}]브레이크 패턴(조합) 이 없거나 내부 구성에 문제가 있습니다.");
            return;
        }
        stepQueue = new Queue<BrakeStep>(brakePattern.steps);
    }

    /// <summary>
    /// 패턴에 따른 주행을 시작한다.
    /// </summary>
    public void StartPattern()
    {
        if (stepQueue == null || stepQueue.Count == 0)
        {
            Debug.LogWarning("실행할 브레이크 패턴(조합)이 없습니다.");
            return;
        }
        if(playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = StartCoroutine(PlayPatternLoop());
    }

    public void StopPattern()
    {
        if(playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = null;
    }

    /// <summary>
    /// Pause 요청을 보낸다 → 현재 패턴 소진 후 대기 상태로 진입
    /// </summary>
    public void RequestPause()
    {
        pauseRequested = true;
    }

    /// <summary>
    /// Resume 요청을 보낸다 → 대기 중이면 다음 패턴 실행
    /// </summary>
    public void RequestResume()
    {
        resumeRequested = true;
    }

    private IEnumerator PlayPatternLoop()
    {
        while (true)
        {
            // 1) 현재 패턴이 남아있으면 계속 소진
            while (stepQueue.Count > 0)
            {
                var step = stepQueue.Dequeue();
                try
                {
                    if (step.action == BrakeAction.BehaviourEffect)
                    {
                        // 거리에 따른 로직 처리
                        StartCoroutine(ApplyStep(LeadCarStateMachine.Instance.GetBehaviourEffect(step)));
                    }
                    else
                    {
                        StartCoroutine(ApplyStep(step));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    throw;
                }
                
                
                // 상태 변화를 감지하며 대기
                float elapsedTime = 0f;
                while (elapsedTime < step.duration)
                {
                    elapsedTime += Time.deltaTime;
                    if (pauseRequested || resumeRequested)
                    {
                        Debug.Log("Pause, Resume 요청들어옴... 아직 대기");
                    }
                    yield return null;
                }
                
                // Debug.Log($"브레이크 스탭 소진: {step.action}, {step.duration}초");

                if (pauseRequested)
                {
                    Debug.Log("BrakePatternManager: 일시정지 대기 중...");
                    // 현재 스탭까지 실행 후, Resume 신호 올 때까지 대기
                    LeadCarStateMachine.Instance.SetCanStartRoutine(true);
                    yield return new WaitUntil(() => resumeRequested);
                    LeadCarStateMachine.Instance.SetCanStartRoutine(false);
                    pauseRequested = false;
                    resumeRequested = false;
                }
                // resumeRequested가 true가 되면 대기 루프를 빠져나오므로, 여기서 추가 처리는 필요 없음.
            }
            Debug.Log("패턴 소진 완료, 다음 패턴 로드 중...");

            // 2) 다음 패턴 로드
            LoadNextPattern();
            if (stepQueue == null || stepQueue.Count == 0)
            {
                Debug.LogWarning("다음 브레이크 패턴(조합)이 없습니다. 루프 종료");
                break;
            }
            
            // 3) 다음 패턴 재생까지 대기 시간
            Debug.Log("전방 차량 속도 100으로 올림..");
            StartCoroutine(LeadCarStateMachine.Instance.LeadCarRearrangeRoutine(3));
            
            // yield return new WaitForSeconds(Random.Range(3, 7)); // 기존 코드

            // 상태 변화를 감지하며 랜덤 대기
            float randomWaitTime = 7;
            float waitElapsedTime = 0f;
            Debug.Log($"다음 패턴 시작까지 {randomWaitTime} 대기...");
            while (waitElapsedTime < randomWaitTime && !pauseRequested && !resumeRequested)
            {
                waitElapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 대기 중 Resume 요청이 들어왔으면 처리 후 다음 패턴으로 바로 진행
            if (resumeRequested)
            {
                Debug.Log("BrakePatternManager: 대기 중 Resume 요청 감지, 다음 패턴 진행");
                resumeRequested = false; // 요청 처리 완료
            }
            // pauseRequested는 step duration 대기에서 이미 처리됨
        }
        float targetSpeedMS = CarUtils.ConvertKmHToMS(80);
        yield return LeadCarStateMachine.Instance.leadCarController.AccelerateToTargetSpeed(targetSpeedMS, 5);
        StartCoroutine(LeadCarStateMachine.Instance.leadCarController.MaintainSpeed());
    }

    private IEnumerator ApplyStep(BrakeStep step)
    {
        // 속도 유지 루틴이 있으면 정지
        if (LeadCarStateMachine.Instance.otherCarCoroutine_MaintainTargetSpeed != null)
        {
            StopCoroutine(LeadCarStateMachine.Instance.otherCarCoroutine_MaintainTargetSpeed);
            LeadCarStateMachine.Instance.otherCarCoroutine_MaintainTargetSpeed = null;
        }
        descriptionText.text = $"Action: {step.action}, \nDuration: {step.duration}, \nMagnitude: {step.magnitude}"; 
        Debug.Log($"Action: {step.action}, Duration: {step.duration}, Magnitude: {step.magnitude}");

        IEnumerator movementRoutine;
        switch (step.action)
        {
            case BrakeAction.Brake:
                BrakeVisualizeManager.instance.
                    ActiveLight(DrivingDataManager.Instance.brakeLightType, step.magnitude, step.duration);
                movementRoutine =
                    LeadCarStateMachine.Instance.leadCarController
                        .AccelerateWithFixedAcceleration(step.magnitude, step.duration);
                yield return movementRoutine;
                break;
            case BrakeAction.Maintain:
                movementRoutine =
                    LeadCarStateMachine.Instance.leadCarController
                        .MaintainSpeedForWaitTime(step.duration);
                yield return movementRoutine;
                
                break;
            case BrakeAction.Accelerate:
                movementRoutine =
                    LeadCarStateMachine.Instance.leadCarController
                        .AccelerateWithFixedAcceleration(step.magnitude, step.duration);
                yield return movementRoutine;
                break;
        }
    }

    public void LoadNextPattern()
    {
        startPatternIndex++;
        if(startPatternIndex >= randomizedOrder.Length) return;
        LoadPattern(randomizedOrder[startPatternIndex]);
    }
    
    #endregion
}
