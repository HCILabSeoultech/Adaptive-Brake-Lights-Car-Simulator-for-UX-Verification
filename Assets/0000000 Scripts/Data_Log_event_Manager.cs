using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Data_Log_event_Manager : MonoBehaviour
{
    private StreamWriter writer;

    // 게임이 시작될 때 호출
    private void Awake()
    {
        // 로그 파일 이름을 현재 시간으로 설정 (년-월-일-시-분-초)
        string fileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";

        // 로그 파일 경로 설정 (Application.persistentDataPath를 사용하여 절대 경로 지정)
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        // 파일 생성 및 작성 준비
        writer = new StreamWriter(filePath);

        // CSV 파일의 첫 번째 행에 헤더 추가 (날짜, 시간, 로그 메시지)
        writer.WriteLine("Date,Time,Log");

        // 로그 메시지 기록 이벤트 등록
        Application.logMessageReceived += saveLog;

        Debug.Log("log1 save start");
    }

    // 게임이 종료될 때 호출 (또는 게임 오브젝트가 비활성화될 때 호출)
    private void OnDisable()
    {
        // 로그 기록 종료
        Debug.Log("log1 save end");

        // 이벤트 등록 해제
        Application.logMessageReceived -= saveLog;

        // 스트림 닫기
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
        }
    }

    // 로그 메시지를 파일에 저장하는 메서드
    private void saveLog(string logString, string stackTrace, LogType type)
    {
        string dateTime = DateTime.Now.ToString("yyyy-MM-dd");
        string currentTime = DateTime.Now.ToString("HH:mm:ss.fff");

        // 로그를 CSV 형식으로 저장 (날짜, 시간, 로그 메시지)

        string result;
        result = String.Join(",", dateTime, currentTime, logString);


        writer.WriteLine(result);
    }
}
