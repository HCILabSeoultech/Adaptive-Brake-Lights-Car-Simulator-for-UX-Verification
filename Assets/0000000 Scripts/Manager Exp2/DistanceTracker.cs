using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DistanceTracker : MonoBehaviour
{
    [Header("현재 속도 (km/h)")]
    [Tooltip("외부에서 1초마다 갱신되는 속도 값을 넣어주세요.")]
    public float speedKmh;

    [Header("목표 거리 (km)")]
    [Tooltip("슬라이더의 최대값으로도 사용됩니다.")]
    public float targetDistance = 100f;

    [Header("이동 거리 표시용 텍스트 (선택)")]
    public TextMeshProUGUI distanceText;

    [Header("진행도 표시용 슬라이더")]
    public Slider distanceSlider;

    // 누적 이동 거리 (km)
    private float distanceKm = 0f;

    public GameObject gameOverUI;
    void Start()
    {
        distanceKm = 0f;
        // 슬라이더 설정
        if (distanceSlider != null)
        {
            distanceSlider.minValue = 0f;
            distanceSlider.maxValue = targetDistance;
            distanceSlider.value    = distanceKm;
        }
        UpdateUI();
    }

    void Update()
    {
        // 거리 계산: speed (km/h) × 시간(h)
        distanceKm += speedKmh * (Time.deltaTime / 3600f);
        if (distanceKm >= targetDistance)
        {
            GameOver();
            return;
        }
        // 슬라이더 업데이트
        if (distanceSlider != null)
            distanceSlider.value = Mathf.Min(distanceKm, targetDistance);

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (distanceText != null)
            distanceText.text = $"{distanceKm:F2} / {targetDistance:F2} km";
    }

    /// <summary>
    /// 외부에서 누적 이동 거리를 가져가고 싶을 때 호출
    /// </summary>
    public float GetDistance()
    {
        return distanceKm;
    }

    void GameOver()
    {
        gameOverUI.SetActive(true);
        DrivingDataManager.Instance.gameOver = true;
        AudioManager.Instance.PlayEndDrivingAudio();
    }
}