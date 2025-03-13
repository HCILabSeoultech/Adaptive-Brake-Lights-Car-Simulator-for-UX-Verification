using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class UserDataLoggingManager : MonoBehaviour
{
    public static UserDataLoggingManager Instance;
    public string filePath = "";
    public float dataSaveTime = 2.0f; // CanWrite을 유지할 시간 (초)
    private bool canWrite = false;
    private float canWriteStartTime; // CanWrite이 true가 된 시간

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
            // WriteCsvRow();
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
        else
        {
            File.Delete(filePath);
            CreateUserDataCsv();
        }
    }

    private void CreateUserDataCsv()
    {
        List<string> lines = new List<string>(){};
        File.WriteAllLines(filePath, lines);
        Debug.Log($"Created User Data (CSV Format), PATH: {filePath}");
    }
    
    
    
    public void WriteCsvRow(
        float interval, bool collision,
        float safeDistance, float holdTime, DateTime currentTime,
        float leadAccel, float testAccel, float accelIntensity,
        float brakeIntensity, float vehicleDistance)
    {
        // CSV 한 줄 데이터 생성 (콤마로 구분)
        string csvRow = $"{DrivingScenarioManager.Instance.level},{DrivingScenarioManager.Instance.brakePatternTypes[DrivingScenarioManager.Instance._currentBrakePatternIndex]},{interval},{collision},{safeDistance},{holdTime}," +
                        $"{currentTime:yyyy-MM-dd HH:mm:ss},{leadAccel},{testAccel}," +
                        $"{accelIntensity},{brakeIntensity},{vehicleDistance}";

        // 파일에 한 줄 추가 (Append 모드)
        File.AppendAllText(filePath, csvRow + Environment.NewLine);
    }
}
// 수준	브레이크 유형	간격	충돌여부	안전거리 유지 시간	현재 시간	선두 차량 가속도	실험 차량 가속도	엑셀 세기	브레이크 세기	차량 간 거리