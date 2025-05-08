using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrivingDataManager : MonoBehaviour
{
    [Header("실험자 정보 입력")]
    public string userName = "";
    public string userNumber = "";
    public Gender gender;
    public DrivingLevel drivingLevel;
    
    [Header("주행 정보 설정")]
    public BrakeLightType brakeLightType;
    public int trialNumber;
    
    public static DrivingDataManager Instance;

    private void Awake()
    {
        if(Instance == null) Instance = this;
    }
}
