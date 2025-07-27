using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DrivingDataManager : MonoBehaviour
{
    [Header("실험자 정보 입력")]
    public string userNumber = "";
    public string userName = "";
    public Gender gender;
    public DrivingLevel drivingDuration;
    
    [Header("주행 정보 설정")]
    public BrakeLightType brakeLightType;
    public int trialNumber;
    
    public static DrivingDataManager Instance;

    private void Awake()
    {
        if(Instance == null) Instance = this;
    }

    public string filePath = "";
    public float dataSaveTime = 4.5f; // CanWrite을 유지할 시간 (초)
    private bool canWrite = false;
    private float canWriteStartTime; // CanWrite이 true가 된 시간
    public SpeedAndGearUIManager speedAndGearUIManager;
    public bool CanWrite
    {
        get => canWrite;
        set
        {
            if (value && !canWrite) // 처음 true가 되는 순간 기록
            {
                canWriteStartTime = Time.time;
            }
            canWrite = value;
        }
    }

    private void Start()
    {
        if (Instance != null)
        {
            InitUserData();
        }
    }


    private void FixedUpdate()
    {
        WriteCsvRow();
    }

    public void SetCanWrite(bool can)
    {
        CanWrite = can;
    }
    private void InitUserData()
    {
        // TODO: {참가자 번호}_{참가자 이름}.csv 파일 생성
        // DrivingScenarioManager.Instance.userName
        // DrivingScenarioManager.Instance.userNumber
        string fileName = userNumber + "_" + userName + "_" + gender + "_" + drivingDuration + "_Unity.csv";  
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        if (!File.Exists(filePath))
        {
            CreateUserDataCsv();
        }
        /*else
        {
            File.Delete(filePath);
            CreateUserDataCsv();
        }*/
    }

    private void CreateUserDataCsv()
    {
        List<string> lines = new List<string>()
        {
            "브레이크 유형,실시간 앞차 간격,현재 시간,충돌여부,선두 차량 가속도,실험 차량 가속도,선두 차량 속도,실험 차량 속도,엑셀 세기,브레이크 세기,"
        };

        using (StreamWriter writer = new StreamWriter(filePath, false, new System.Text.UTF8Encoding(true)))
        {
            foreach (string line in lines)
            {
                writer.WriteLine(line);
            }
        }
        Debug.Log($"Created User Data (CSV Format), PATH: {filePath}");
    }
    
    
    public void WriteCsvRow()
    {
        string csvRow = $"{brakeLightType}, {LeadCarStateMachine.Instance.GetCurrentDistance()}," +
                        $"{DateTime.Now:HH:mm:ss:fff},{LeadCarStateMachine.Instance.currentState},{LeadCarStateMachine.Instance.leadCarController.GetLeadCarAcceleration()},{LeadCarStateMachine.Instance.playerCarController.GetPlayerCarAcceleration()}," +
                        $"{speedAndGearUIManager.aheadCarSpeed},{speedAndGearUIManager.playerCarSpeed}," + 
                        $"{LeadCarStateMachine.Instance.playerCarController.GetForwardInput0to1()},{LeadCarStateMachine.Instance.playerCarController.GetBrakeInput0to1()}";

        using (StreamWriter writer = new StreamWriter(filePath, true, new System.Text.UTF8Encoding(true)))
        {
            writer.WriteLine(csvRow);
        }
        // Debug.Log(csvRow);
    }
}
