using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Z_SCORE_Slider_Controller : MonoBehaviour
{
    [SerializeField] private Slider SCR_Z_slider; // SCR �����̴� ������Ʈ
    [SerializeField] private Slider GSR_Z_slider; // GSR �����̴� ������Ʈ
    [SerializeField] private SCR_Z_score_Manager z_scoreManager; // z_score ���� ��ũ��Ʈ
    [SerializeField] private Image[] fillImage; // �����̴� Fill ���� �̹���
    
    private float scr_z_score_max = 4;
    private float gsr_z_score_max = 2;

    void Start()
    {
        SCR_Z_slider.maxValue = scr_z_score_max; // �����̴��� �ִ밪 ����
    }

    void Update()
    {
        if (z_scoreManager != null)
        {
            float scr_z_score = z_scoreManager.SCR_Z_SCORE;// scr_zscore �� ȣ��
            float gsr_z_score = z_scoreManager.GSR_Z_SCORE;// gsr_zscore �� ȣ��

            // �����̴� �� ������Ʈ
            SCR_Z_slider.value = Mathf.Clamp(scr_z_score, 0f, scr_z_score_max);
            GSR_Z_slider.value = Mathf.Clamp(gsr_z_score, 0f, gsr_z_score_max);

            // Fill ���� ������Ʈ
            UpdateFillColor(scr_z_score,scr_z_score_max,0); //scr z_score ���������Ʈ
            UpdateFillColor(gsr_z_score,gsr_z_score_max,1); //gsr z_score ���������Ʈ 
        }
        else
        {
            Debug.Log("z_score�� slider�� ���޵��� ����");
        }
    }


    void UpdateFillColor(float z_score, float z_score_max, int n)
    {
        // z_score ���� ����: 0 �̸��� 0, 4 �ʰ��� 4�� ó��
        float clampedValue = Mathf.Clamp(z_score, 0f, z_score_max);
        float normalizedValue = clampedValue / z_score_max; // 0~4 ���� 0~1�� ����ȭ

        Color color;

        if (normalizedValue <= 0.25f) // 0 ~ 1 (�ʷ� �� �����)
        {
            color = Color.Lerp(Color.green, new Color(0.5f, 1f, 0f), normalizedValue / 0.25f);
        }
        else if (normalizedValue <= 0.5f) // 1 ~ 2 (����� �� ���)
        {
            color = Color.Lerp(new Color(0.5f, 1f, 0f), Color.yellow, (normalizedValue - 0.25f) / 0.25f);
        }
        else if (normalizedValue <= 0.75f) // 2 ~ 3 (��� �� ��Ȳ)
        {
            color = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (normalizedValue - 0.5f) / 0.25f);
        }
        else // 3 ~ 4 (��Ȳ �� ����)
        {
            color = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (normalizedValue - 0.75f) / 0.25f);
        }

        // Fill ���� ���� ������Ʈ
        if (fillImage[n] != null)
        {
            fillImage[n].color = color;
        }
    }

}
