using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrakeSystem : MonoBehaviour
{
    public static BrakeSystem instance;
    public LEDController ledController;
    private ILightBehavior standard, frequency, brightness, area;

    [Header("Properties")] public float frequencyValue;
    private void Awake()
    {
        if (instance == null) instance = this;
    }
    
}
