using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarUtils
{
    /// <summary>
    /// km/h 단위의 속도를 m/s 단위의 속도 값으로 반환한다. 
    /// </summary>
    /// <param name="speedKmH">목표 속도를 km/h 값으로 받는다. </param>
    /// <returns></returns>
    public static float ConvertKmHToMS(float speedKmH)
    {
        return speedKmH / 3.6f;
    }

    public static List<float> GetRandomizedAccelerationsOrder()
    {
        List<float> accelerationOrder = new List<float>();
        
        // 1.0f부터 8.0f까지 리스트에 추가
        for (float i = -1.0f; i >= -8.0f; i -= 1.0f)
        {
            accelerationOrder.Add(i);
        }
        
        // 리스트 섞기 (Fisher-Yates Shuffle 알고리즘 사용)
        System.Random random = new System.Random();
        int count = accelerationOrder.Count;
        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (accelerationOrder[i], accelerationOrder[j]) = (accelerationOrder[j], accelerationOrder[i]); // Swap
        }

        Debug.Log(string.Join(", ", accelerationOrder));
        return accelerationOrder;
    }

    public static List<float> GetRandomizedAccelerationsOrder(Level level)
    {
        List<float> accelerationOrder = new List<float>();
        if (level == Level.수준2)
        {
            return null;
            
        }else if (level == Level.수준3)
        {
            return null;
        }
        else
        {
            return null;
        }
    }
    
    public static List<(float deceleration, float distance)> GetRandomDecelerationDistanceList(Level level)
    {
        List<float> decelerationLevels = new List<float>();

        if (level == Level.수준2)
        {
            decelerationLevels.Add(-2f);
            decelerationLevels.Add(-6f);
        }
        else
        {
            decelerationLevels.Add(-2f);
            decelerationLevels.Add(-4f);
            decelerationLevels.Add(-6f);
        }

        float[] possibleValues = { 20f, 40f, 60f };
        List<(float, float)> allCombinations = new List<(float, float)>();

        // 모든 조합 생성
        foreach (int decel in decelerationLevels)
        {
            foreach (int value in possibleValues)
            {
                allCombinations.Add((decel, value));
            }
        }

        // 셔플
        System.Random rnd = new System.Random();
        for (int i = allCombinations.Count - 1; i > 0; i--)
        {
            int swapIndex = rnd.Next(i + 1);
            // 구조 분해로 스왑 - 튜플 문법
            (allCombinations[i], allCombinations[swapIndex]) = (allCombinations[swapIndex], allCombinations[i]);
        }

        foreach (var combination in allCombinations)
        {
            Debug.Log(combination);
        }
        return allCombinations;
    }
}
