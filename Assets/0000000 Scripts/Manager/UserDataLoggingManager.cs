using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class UserDataLoggingManager : MonoBehaviour
{
    public static UserDataLoggingManager Instance;
    public string filePath = "";
    public float dataSaveTime = 2.5f; // CanWrite을 유지할 시간 (초)
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
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
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
        if (CanWrite)
        {
            if (Time.time - canWriteStartTime >= dataSaveTime)
            {
                CanWrite = false; // 시간이 지나면 false로 변경
            }
            WriteCsvRow();
        }
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
        string fileName = DrivingScenarioManager.Instance.userNumber + "_" + DrivingScenarioManager.Instance.userName+".csv";  
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
            "수준,브레이크 유형,간격,현재 시간,충돌여부,선두 차량 가속도,실험 차량 가속도,선두 차량 속도,실험 차량 속도,엑셀 세기,브레이크 세기,차량 간 거리"
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
        string csvRow = $"{DrivingScenarioManager.Instance.level},{DrivingScenarioManager.Instance.brakePatternTypes[DrivingScenarioManager.Instance._currentBrakePatternIndex]}, {DrivingScenarioManager.Instance.startConditionDistance}," +
                        $"{DateTime.Now:HH:mm:ss:fff},{DrivingScenarioManager.Instance.IsConflictWithOtherCar()},{DrivingScenarioManager.Instance.otherCarController.targetAccelderation},{DrivingScenarioManager.Instance.playerCarController.GetPlayerCarAcceleration()}," +
                        $"{speedAndGearUIManager.aheadCarSpeed},{speedAndGearUIManager.playerCarSpeed}," + 
                        $"{DrivingScenarioManager.Instance.playerCarController.GetForwardInput0to1()},{DrivingScenarioManager.Instance.playerCarController.GetBrakeInput0to1()},{DrivingScenarioManager.Instance.GetCurrentDistance()}";

        using (StreamWriter writer = new StreamWriter(filePath, true, new System.Text.UTF8Encoding(true)))
        {
            writer.WriteLine(csvRow);
        }
        // Debug.Log(csvRow);
    }
}
// 수준, 브레이크 유형, 간격, 충돌여부, 안전거리유지중인가, 현재 시간, 선두 차량 가속도, 실험 차량 가속도, 엑셀 세기, 브레이크 세기, 차량 간 거리