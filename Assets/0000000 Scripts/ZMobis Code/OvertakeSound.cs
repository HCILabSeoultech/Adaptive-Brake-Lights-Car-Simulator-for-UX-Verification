using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OvertakeSound : MonoBehaviour
{
    public AudioClip collisionSound;  // 충돌할 때 재생할 소리
    public AudioSource audioSource;  // 오디오 소스 컴포넌트

    void Start()
    {
        // 이 오브젝트에 붙어 있는 AudioSource 컴포넌트를 가져옴
        audioSource = GetComponent<AudioSource>();
    }

    // 물체가 다른 물체에 충돌할 때 호출되는 함수
    void OnTriggerEnter(Collider col)
    {
        // 충돌할 때 소리를 재생
        if (col.gameObject.name == "test2_stress_speedUp")
        {
            StartCoroutine(delayPlay());
        }
    }

    IEnumerator delayPlay()
    {
        yield return new WaitForSeconds(4f);
        audioSource.clip = collisionSound;
        audioSource.Play();
    }
}
