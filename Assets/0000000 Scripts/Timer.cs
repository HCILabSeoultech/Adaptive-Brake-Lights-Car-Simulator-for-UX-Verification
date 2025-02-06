using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Timer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    
    [Header(("Time Limit"))] public float timeLimit;
    private float _remainingTime; // 남은 시간을 초로 저장
    private int lastDisplayedSeconds; // 마지막으로 표시한 초를 저장해서 중복 계산 방지\
   
    // 시간알려줌
    [SerializeField] private AudioSource audiosourceTime_Only;
    [SerializeField] private AudioClip[] audioClipTTSTime_Only;
    private bool Paused = false;


    // 차가 움직이는지 안움직이는 지 확인!!
    [SerializeField] private Controller controller;
    enum Time_Only
    {
        Only10,
        Only5
        
    }
    void Start()
    {
        InitializeTimer();
    }

    public void InitializeTimer()
    {
        Debug.Log("Initialize Timer");
        _remainingTime = timeLimit * 60; // 분을 초로 변환
        UpdateTimerText(); // 처음 텍스트를 업데이트
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0)) //트레이닝 모드
        {
            Paused = true; // timer 기능을 멈춘다.
            timeLimit = 3;
            _remainingTime = timeLimit * 60; // 분을 초로 변환
            UpdateTimerText();

        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) // 실제 주행
        
        { 
            Paused = true; // timer 기능을 멈춘다.
            timeLimit = 25;
            _remainingTime = timeLimit * 60; // 분을 초로 변환
            UpdateTimerText();
        }

        /*
        if(Paused == true && controller.speedValue > 0)
        {
            Paused = false;
        }*/


        if (!Paused)
        {
            // 멈추지 않은 경우에만 실행
            UpdateTimer();
        }
       
    }

    // 시간을 mm:ss 형식으로 업데이트하는 함수
    void UpdateTimerText()
    {
        int minutes = Mathf.FloorToInt(_remainingTime / 60);
        int seconds = Mathf.FloorToInt(_remainingTime % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    void UpdateTimer()
    {
        if (_remainingTime > 0)
        {
            _remainingTime -= Time.deltaTime;

            // 현재 초를 int형으로 표현
            int currentSeconds = Mathf.FloorToInt(_remainingTime);

            if (_remainingTime <= 600f && _remainingTime >= 599f) //10분
            {
                audiosourceTime_Only.clip = audioClipTTSTime_Only[(int)Time_Only.Only10];
                audiosourceTime_Only.Play();

            }

            if (_remainingTime <= 300f && _remainingTime >= 299f) //5분
            {
                audiosourceTime_Only.clip = audioClipTTSTime_Only[(int)Time_Only.Only5];
                audiosourceTime_Only.Play();

            }

            // 초 단위에서 변화가 있을 때만 텍스트 업데이트
            if (currentSeconds != lastDisplayedSeconds)
            {
                UpdateTimerText();
                lastDisplayedSeconds = currentSeconds; // 업데이트된 시간을 저장
            }




        }
        else
        {
            _remainingTime = 0;
            UpdateTimerText();
        }
    }

    



}
