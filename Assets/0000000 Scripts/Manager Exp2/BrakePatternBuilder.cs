using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrakePatternBuilder : MonoBehaviour
{
    [Header("여러 개의 브레이크 패턴을 정의할 수 있음.")]
    public BrakePattern[] brakePatterns;

    public BrakePattern GetPattern(int index)
    {
        if (index < 0 || index >= brakePatterns.Length) return null;
        return brakePatterns[index];
    }
}

[Serializable]
public enum BrakeAction
{
    Brake,      // 제동
    Maintain,   // 속도 유지
    Accelerate  // 감속
}

[Serializable]
public class BrakeStep
{
    [Tooltip("이 단계에서 취할 동작")]
    public BrakeAction action;

    [Tooltip("이 동작을 유지할 시간(초)")] 
    public float duration;
    
    [Tooltip("제동, 가속의 강도, 제동일 땐 음수, 가속일 땐 양수, 속도 유지는 0")]
    public float magnitude;
}

[Serializable]
public class BrakePattern
{
    [Tooltip("패턴 구분 이름")] 
    public string name;
    
    [Tooltip("순차적으로 실행될 브레이크/가속 단계들")]
    public List<BrakeStep> steps = new List<BrakeStep>();
}
