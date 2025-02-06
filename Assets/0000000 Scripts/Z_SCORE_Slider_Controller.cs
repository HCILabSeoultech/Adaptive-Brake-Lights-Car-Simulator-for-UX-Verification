using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Z_SCORE_Slider_Controller : MonoBehaviour
{
    [SerializeField] private Slider SCR_Z_slider; // SCR 슬라이더 오브젝트
    [SerializeField] private Slider GSR_Z_slider; // GSR 슬라이더 오브젝트
    [SerializeField] private SCR_Z_score_Manager z_scoreManager; // z_score 관리 스크립트
    [SerializeField] private Image[] fillImage; // 슬라이더 Fill 색상 이미지
    
    private float scr_z_score_max = 4;
    private float gsr_z_score_max = 2;

    void Start()
    {
        SCR_Z_slider.maxValue = scr_z_score_max; // 슬라이더의 최대값 설정
    }

    void Update()
    {
        if (z_scoreManager != null)
        {
            float scr_z_score = z_scoreManager.SCR_Z_SCORE;// scr_zscore 값 호출
            float gsr_z_score = z_scoreManager.GSR_Z_SCORE;// gsr_zscore 값 호출

            // 슬라이더 값 업데이트
            SCR_Z_slider.value = Mathf.Clamp(scr_z_score, 0f, scr_z_score_max);
            GSR_Z_slider.value = Mathf.Clamp(gsr_z_score, 0f, gsr_z_score_max);

            // Fill 색상 업데이트
            UpdateFillColor(scr_z_score,scr_z_score_max,0); //scr z_score 색상업데이트
            UpdateFillColor(gsr_z_score,gsr_z_score_max,1); //gsr z_score 색상업데이트 
        }
        else
        {
            Debug.Log("z_score가 slider에 전달되지 않음");
        }
    }


    void UpdateFillColor(float z_score, float z_score_max, int n)
    {
        // z_score 범위 제한: 0 미만은 0, 4 초과는 4로 처리
        float clampedValue = Mathf.Clamp(z_score, 0f, z_score_max);
        float normalizedValue = clampedValue / z_score_max; // 0~4 값을 0~1로 정규화

        Color color;

        if (normalizedValue <= 0.25f) // 0 ~ 1 (초록 → 연녹색)
        {
            color = Color.Lerp(Color.green, new Color(0.5f, 1f, 0f), normalizedValue / 0.25f);
        }
        else if (normalizedValue <= 0.5f) // 1 ~ 2 (연녹색 → 노랑)
        {
            color = Color.Lerp(new Color(0.5f, 1f, 0f), Color.yellow, (normalizedValue - 0.25f) / 0.25f);
        }
        else if (normalizedValue <= 0.75f) // 2 ~ 3 (노랑 → 주황)
        {
            color = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (normalizedValue - 0.5f) / 0.25f);
        }
        else // 3 ~ 4 (주황 → 빨강)
        {
            color = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (normalizedValue - 0.75f) / 0.25f);
        }

        // Fill 영역 색상 업데이트
        if (fillImage[n] != null)
        {
            fillImage[n].color = color;
        }
    }

}
