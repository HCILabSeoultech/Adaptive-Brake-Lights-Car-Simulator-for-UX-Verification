using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class camera : MonoBehaviour
{
    public RawImage webcamDisplay;
    // Start is called before the first frame update
    void Start()
    {
        // 모든 웹캠 장치 목록을 가져옵니다.
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.Log("No webcam detected.");
            return;
        }

        // 첫 번째 웹캠을 사용하여 WebCamTexture를 생성합니다.
        WebCamTexture webcamTexture = new WebCamTexture(devices[0].name);

        // RawImage 컴포넌트에 웹캠 영상을 할당합니다.
        webcamDisplay.texture = webcamTexture;

        // 웹캠을 시작합니다.
        webcamTexture.Play();
    }
}
