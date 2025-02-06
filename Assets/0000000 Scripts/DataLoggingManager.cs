using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

//  시간, 엔진, 브레이크, 속도 : float
public class DataLoggingManager : MonoBehaviour
{
    public static DataLoggingManager Instance;
    public List<string> lines = new List<string>();
    private int _dataNumber = 0;
    public bool isFinished = false;    private void Awake()
    {
        if(Instance == null) Instance = this;
    }

    public void CreateInitialData()
    {
        // 파일 이름 지정 (0번 부터 시작)
        string fileName = $"Data_{_dataNumber}.csv";

        // dataPath 지정
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        while (File.Exists(filePath)) // 경로에 해당 파일이 존재하지 않을 때 까지 dataNumber 증가
        {
            _dataNumber++;
            fileName = $"Data_{_dataNumber}.csv";
            filePath = Path.Combine(Application.persistentDataPath, fileName);
        }
        
        // 변화된 data Number을 바탕으로 다시 파일 이름, 파일 경로 생성
        fileName = $"Data_{_dataNumber}.csv";
        filePath = Path.Combine(Application.persistentDataPath, fileName);
        //Debug.Log("경로 : " + filePath);
        lines = new List<string>()
        {
            $"{fileName},{DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss.fff")}"
        };
        
        File.WriteAllLines(filePath, lines);
        
        // filePath 로그 찍기 (디버깅 용도)
        //Debug.Log(filePath);
    }

    // TODO : event 시작,
    // 종료 시점에 호출코드 추가
    // if (!DataLoggingManager.Instance.isFinished)
            // DataLoggingManager.Instance.AddLogData(dateText, timeText, speedValue, parkInput, SteeringInput*450f, "@@@ 이벤트 시작(또는 종료)");

    // 초기화 이후 매 프레임 호출될 메서드
    public void AddLogData(string date, string time, float speedValue, float breakValue, float handleValue, string eventTime = null)
    {
        string result;
        if (eventTime == null)
        {
            result = String.Join(",", date, time, speedValue.ToString(), breakValue.ToString(), handleValue.ToString());
        }
        else
        {
            result = String.Join(",", date, time, speedValue.ToString(), breakValue.ToString(), handleValue.ToString(), eventTime);
        }

        lines.Add(result);
    }
    
    // TODO : 끝나는 시점에 0번 줄 마지막에 주행 시간완료 까지 입력

    // 실험이 종료되는 시점에 호출할 메서드
    public void SaveLoggedData()
    {
        string path = Application.persistentDataPath;

        DirectoryInfo dirInfo = new DirectoryInfo(path);
        
        FileInfo[] files = dirInfo.GetFiles($"Data_{_dataNumber}.csv");

        if (files.Length == 0)
        {
            Debug.Log($"데이터 경로{path}에 Data_{_dataNumber}.csv 파일이 존재하지 않습니다.");
        }
        else
        {
            Debug.Log("Loaded " + files[0].Name);
        }
        // 0번 파일 
        // 변경된 데이터를 파일에 다시 쓰기
        using (StreamWriter writer = new StreamWriter(files[0].FullName, false))
        {
            foreach (string line in lines)
            {
                writer.WriteLine(line);
            }
        }

        isFinished = true;
    }
}
