using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 브레이크 패턴(조합)에 따른 선두 차량 제어를 위한 매니저 클래스
/// </summary>
public class BrakePatternManager : MonoBehaviour
{
    // TODO: Queue 자료구조로 동작해야 할 행동을 제어, BrakePatternBuilder의 brakePatterns를 랜덤으로
    // TODO: 일시정지 후 다시 재생이 가능해야 함.
    public BrakePatternBuilder brakePatternBuilder;
    public int[] randomizedOrder;

    #region Initialize
    private void Awake()
    {
        Init();
    }
    
    void Init()
    {
        SetRandomizedOrder();
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
    
    
}
