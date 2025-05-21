using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

// FSM 상태용 enum
public enum DistanceState
{
    Collision,  // 사고 발생
    tooClose,   // 잘 따라옴
    normal,     // 평범
    tooFar,      // 못 따라옴
    changeLine  // 차선 변경 이벤트
}
public class LeadCarStateMachine : MonoBehaviour
{
    public static LeadCarStateMachine Instance;
    [Header("차량 제어기")]
    public Controller playerCarController;
    public LeadCarController leadCarController;
    public TrafficManager trafficManager; 
    
    [Header("거리 임계값 설정")]
    public float collisionThreshold = 4.5f;
    public float closeThreshold = 20f;
    public float farThreshold = 50f;
    public TextMeshProUGUI distanceText;
    
    [Header("상태 전환 지연 시간(초)")] 
    public float stateChangeDelay = 10f;
    // 후보 상태와 그 지속 시간
    [SerializeField] private float stateTimer = 0f;
    private float changeLineTimer = 0f;
    private bool changeLine = false;
    // public float recentSwitchedTime = 0f;
    
    [Header("현재 상태, 후보 상태")]
    public DistanceState currentState = DistanceState.normal;
    public DistanceState candidateState;

    [Header("상태에 따른 조절할 거리의 크기")] 
    [SerializeField]
    private float tooCloseDistanceOffset = 50f;  // tooClose 상태일 때 뒤로 물러날 거리 (m)

    [SerializeField]
    private float tooFarDistanceOffset = 30f;    // tooFar 상태일 때 다가갈 거리 (m)


    private Dictionary<DistanceState, Func<IEnumerator>> stateRoutines;
    private Coroutine stateCoroutine;
    public Coroutine otherCarCoroutine_MaintainTargetSpeed;
    public bool canStartRoutine = false;
    
    private void Awake()
    {
        Init();
    }
    void Init()
    {
        if(Instance == null) Instance = this;
        
        // 상태 -> 코루틴 매핑
        stateRoutines = new Dictionary<DistanceState, Func<IEnumerator>>()
        {
            { DistanceState.tooClose, TooCloseRoutine },
            { DistanceState.normal, NormalRoutine },
            { DistanceState.tooFar, TooFarRoutine },
            { DistanceState.Collision, CollisonRoutine },
            { DistanceState.changeLine, ChangeLine2Routine }
        };
        
        // 처음의 후보 상태를 현재 상태로 초기화
        candidateState = currentState;
    }

    private void Start()
    {
        StartCoroutine(LeadCarStartRoutine());
    }

    public BrakeStep GetBehaviourEffect(BrakeStep brakeStep)
    {
        var behaviourEffect = brakeStep;

        if (GetCurrentDistance() < closeThreshold)
        {
            behaviourEffect.action = BrakeAction.Accelerate;
            behaviourEffect.magnitude = 2;
        }else if (GetCurrentDistance() > farThreshold)
        {
            behaviourEffect.action = BrakeAction.Brake;
            behaviourEffect.magnitude = -2;
        }
        else
        {
            behaviourEffect.action = BrakeAction.Maintain;
        }
        
        return behaviourEffect;
    }
    
    public void SetCanStartRoutine(bool canStart)
    {
        canStartRoutine = canStart;
    }

    public IEnumerator LeadCarStartRoutine()
    {
        AudioManager.Instance.PlayStartDrivingAudio();
        float targetSpeedMS = CarUtils.ConvertKmHToMS(100);
        yield return leadCarController.AccelerateToTargetSpeed(targetSpeedMS, 10);
        otherCarCoroutine_MaintainTargetSpeed = StartCoroutine(leadCarController.MaintainSpeed());
        BrakePatternManager.Instance.StartPattern();   
    }
    public IEnumerator LeadCarRearrangeRoutine(float duration, float targetSpeedKmh = 100)
    {
        float targetSpeedMS = CarUtils.ConvertKmHToMS(targetSpeedKmh);
        yield return leadCarController.AccelerateToTargetSpeed(targetSpeedMS, duration);
        otherCarCoroutine_MaintainTargetSpeed = StartCoroutine(leadCarController.MaintainSpeed());
    }
    #region FSM

    private void Update()
    {
        // 매 프레임, 거리 기반으로 즉시 판단되는 상태
        DistanceState rawState = GetCurrentState();
        
        if (candidateState != rawState) // 후보 상태와 현재 상태 다름. 후보 상태 변경
        {
            // 후보 상태가 바뀌면 타이머 리셋
            candidateState = rawState;
            stateTimer = 0;
        }
        else if (candidateState != currentState)
        {
            // 같은 후보 상태가 유지되는 동안 타이머 증가
            stateTimer += Time.deltaTime;

            // 상태별로 사용할 딜레이 결정
            float delay;
            switch (candidateState)
            {
                case DistanceState.Collision:
                    delay = 0.05f;
                    break;
                case DistanceState.normal:
                    delay = 3.0f;
                    break;
                default:
                    delay = stateChangeDelay;  // 원래 값 (예: 5초)
                    break;
            }

            // 정해진 시간이 지나면 상태 전환
            if (stateTimer >= delay && (candidateState == DistanceState.Collision))
            {
                SwitchState(candidateState);
            }
        }

        // 차선 변경 3분에 1회만 실시
        changeLineTimer += Time.deltaTime;
        if (changeLineTimer >= 15 && !changeLine)
        {
            SwitchState(DistanceState.changeLine);
            changeLine = true;
        }
    }

    DistanceState GetCurrentState()
    {
        float dist = GetCurrentDistance();

        if (dist < collisionThreshold || dist > 70 - collisionThreshold) {
            distanceText.text = "Distance: " + dist.ToString("0.00") + " Collision!";
            Rigidbody rb = playerCarController.transform.GetComponent<Rigidbody>();
            rb.constraints |= RigidbodyConstraints.FreezePositionX;
            rb.constraints |= RigidbodyConstraints.FreezeRotationX;
            rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            rb.constraints |= RigidbodyConstraints.FreezeRotationZ;
            
            return DistanceState.Collision; 
        }
        /*if (dist < closeThreshold)
        {
            distanceText.text = "Distance: " + dist.ToString("0.00") + " Too Close!";
            return DistanceState.tooClose;
        }
        else if (dist > farThreshold)
        {
            distanceText.text = "Distance: " + dist.ToString("0.00") + " Too Far!";
            return DistanceState.tooFar;
        }*/
        else {
            distanceText.text = "Distance: " + dist.ToString("0.00") + " Normal";
            return DistanceState.normal; 
        }
    }
    
    /// <summary>
    /// 거리 값으로 즉시 상태만 판단해서 반환
    /// </summary>
    float GetCurrentDistance()
    {
        return Vector3.Distance(leadCarController.transform.position, playerCarController.transform.position);
    }

    void SwitchState(DistanceState nextState)
    {
        // 1) 이전 코루틴 중단
        if(stateCoroutine != null) StopCoroutine(stateCoroutine);
        
        // 2) BrakePatternManager에 Pause 요청
        BrakePatternManager.Instance.RequestPause();
        
        // 3) 실제 상태 전환
        currentState = nextState;
        stateTimer = 0;                 // 전환 직후 타이머 리셋
        candidateState = nextState;     // 후보 상태도 동기화
        stateCoroutine = StartCoroutine(stateRoutines[nextState].Invoke());
        Debug.Log($"[FSM] State switched to {nextState}");
        
    }
    
    #endregion
    
    #region DistanceState Routines
  
    // 잘 따라오는 상태 진입, 간격 벌리기
    IEnumerator TooCloseRoutine()
    {
        yield return new WaitUntil(() => canStartRoutine);
        float targetSpeedMS = CarUtils.ConvertKmHToMS(100);
        yield return StartCoroutine(leadCarController.AlignTestCarToSpeedAndGap(targetSpeedMS, tooCloseDistanceOffset, 3));
        BrakePatternManager.Instance.RequestResume();

        // 간격 줄인 다음 Normal 상태로 초기화
        currentState = DistanceState.normal;
    }

    // 정상 상태 진입
    IEnumerator NormalRoutine()
    {
        yield return new WaitUntil(() => canStartRoutine);
        // 처리하지 않음.
        BrakePatternManager.Instance.RequestResume();
    }
    
    // 못 따라오는 상태 진입, 간격 줄이기
    IEnumerator TooFarRoutine()
    {
        yield return new WaitUntil(() => canStartRoutine);
        float targetSpeedMS = CarUtils.ConvertKmHToMS(100);
        yield return StartCoroutine(leadCarController.AlignTestCarToSpeedAndGap(targetSpeedMS, tooFarDistanceOffset, 3));
        BrakePatternManager.Instance.RequestResume();
        
        // 간격 줄인 다음 Normal 상태로 초기화
        currentState = DistanceState.normal;
    }

    IEnumerator CollisonRoutine()
    {
        // TODO: 사고 처리에 대한 논의 필요, 기존 차량의 패턴 초기화? 
        yield return new WaitUntil(() => canStartRoutine);
        Debug.Log("사고 처리 시작");
        AudioManager.Instance.PlayRearrangementAudio();
        
        // 실험 차량 속도 80으로
        float targetSpeedMS = CarUtils.ConvertKmHToMS(80);
        playerCarController.SetDriveMode(PlayerCarController.DrivingMode.Autonomous);
        Vector3 angles = playerCarController.transform.eulerAngles;
        angles.y = 0f;
        playerCarController.transform.eulerAngles = angles;
        Rigidbody rb = playerCarController.transform.GetComponent<Rigidbody>();
        Vector3 pos1 = transform.position;
        pos1.x = 0f;
        transform.position = pos1;
        rb.constraints |= RigidbodyConstraints.FreezePositionX;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX;
        rb.constraints |= RigidbodyConstraints.FreezeRotationY;
        rb.constraints |= RigidbodyConstraints.FreezeRotationZ;

        StartCoroutine(playerCarController.AccelerateToTargetSpeed(targetSpeedMS-1, 5));
        
        // 선두 차량 정렬
        StartCoroutine(LeadCarRearrangeRoutine(5, 80));
        yield return new WaitForSeconds(5);
        
        // 실험 차량 정렬
        StartCoroutine(playerCarController.AlignTestCarToSpeedAndGap(targetSpeedMS, 35, 5));
        yield return new WaitForSeconds(5);
        rb.constraints &= ~RigidbodyConstraints.FreezePositionX;
        rb.constraints &= ~RigidbodyConstraints.FreezeRotationX;
        rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        rb.constraints &= ~RigidbodyConstraints.FreezeRotationZ;
        
        Debug.Log("사고 처리 종료");
        BrakePatternManager.Instance.RequestResume();
        playerCarController.SetDriveMode(PlayerCarController.DrivingMode.BrakeControl);

        currentState = DistanceState.normal;
    }

    IEnumerator ChangeLine2Routine()
    {
        yield return StartCoroutine(leadCarController.AccelerateToTargetSpeed(CarUtils.ConvertKmHToMS(80), 3));
        yield return new WaitUntil(() => canStartRoutine);
        trafficManager.PauseSpawnAndPushBack(20);
        yield return new WaitUntil(() =>
            leadCarController.transform.position.z > trafficManager.aheadVehicleTransform.position.z);
        
        float targetSpeedMS = CarUtils.ConvertKmHToMS(100);
        yield return StartCoroutine(leadCarController.AccelerateToTargetSpeed(targetSpeedMS, 3));
        yield return StartCoroutine(leadCarController.MoveSecondLine());
        BrakePatternManager.Instance.RequestResume();
        trafficManager.ResumeSpawn();
        // 간격 줄인 다음 Normal 상태로 초기화
        currentState = DistanceState.normal;
    }
    
    #endregion

}
