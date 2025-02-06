using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TextBlink : MonoBehaviour
{
    public float        m_interval = 1.0f;
    public TMP_Text     m_text;

	private void OnEnable()
	{
        if (m_text == null)
            m_text = GetComponent<TMP_Text>();

        StartCoroutine(Blink(m_text, m_interval));
    }

	private void OnDisable()
	{
        StopAllCoroutines();
	}

	static IEnumerator Blink(TMP_Text text, float interval)
    {
        if (text == null)
            yield return null;

        bool fadeIn = false;
        while (true)
        {
            text.CrossFadeAlpha(fadeIn ? 1.0f : 0.0f, interval, true);
            yield return new WaitForSeconds(interval);
            fadeIn = !fadeIn;
        }
    }
}
