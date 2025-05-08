using UnityEngine;
using TMPro;

public class TimerController : MonoBehaviour
{
    [Header("초기 제한 시간 (초)")]
    [Tooltip("예: 5분 = 300초")]
    public float timeLimit = 300f;

    [Header("텍스트 출력용 TextMeshProUGUI")]
    public TextMeshProUGUI timerText;

    // 남은 시간
    private float timeRemaining;

    void Start()
    {
        timeRemaining = timeLimit;
        UpdateTimerText();
    }

    void Update()
    {
        timeRemaining -= Time.deltaTime;
        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        bool isNegative = timeRemaining < 0f;
        float t = isNegative ? -timeRemaining : timeRemaining;

        // 양수 구간에선 내림, 음수 구간에선 올림
        int totalSec = isNegative
            ? Mathf.CeilToInt(t)
            : Mathf.FloorToInt(t);

        int minutes = totalSec / 60;
        int seconds = totalSec % 60;

        string sign = isNegative ? "-" : "";

        timerText.text = $"{sign}{minutes}:{seconds:00}";
    }
}