using UnityEngine;
using TMPro;

public class TimerController : MonoBehaviour
{
    [Header("초기 제한 시간 (초)")]
    [Tooltip("예: 5분 = 300초")]
    public float timeLimit = 240f;

    [Header("텍스트 출력용 TextMeshProUGUI")]
    public TextMeshProUGUI timerText;

    // 남은 시간
    private float timeRemaining;
    public bool isBlinking = false;
    void Start()
    {
        timeRemaining = timeLimit;
        UpdateTimerText();
    }

    void Update()
    {
        timeRemaining -= Time.deltaTime;
        UpdateTimerText();
        CheckBlinkingCondition();
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

    private void CheckBlinkingCondition()
    {
        bool isNegative = timeRemaining < 0f;

        if (isNegative && !isBlinking)
        {
            // 음수가 되었고, 아직 깜빡임 시작 안 했다면 Coroutine 시작
            StartCoroutine(BlinkText());
            isBlinking = true;
        }
    }

    private System.Collections.IEnumerator BlinkText()
    {
        Color normalColor = Color.white;  // 기본 색 (필요하면 inspector에서 지정 가능)
        Color blinkColor = Color.red;     // 깜빡일 때 색

        while (true)
        {
            // 빨간색으로 변경
            timerText.color = blinkColor;
            yield return new WaitForSeconds(0.5f);

            // 원래 색으로 변경
            timerText.color = normalColor;
            yield return new WaitForSeconds(0.5f);
        }
    }
}