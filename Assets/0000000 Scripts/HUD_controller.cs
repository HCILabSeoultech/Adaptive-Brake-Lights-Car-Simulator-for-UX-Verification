using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public class HUD_controller : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject HUD_Red;
    public GameObject HUD_Green;
    private bool log16Once = false;
    private bool log17Once = false;
    private bool log18Once = false;
    private bool log19Once = false;
    [SerializeField] private Setting_Manager setting;

    // Hud 탄생 사운드
    [SerializeField] private AudioSource[] test_sound;
    [SerializeField] private AudioClip[] test_soundClip;

    private void Start()
    {
        log16Once = false;
        log17Once = false;
        log18Once = false;
        log19Once = false;
        HUD_Green.SetActive(false);
        HUD_Red.SetActive(false);
        test_sound[0] = GetComponent<AudioSource>(); // 실험 종료 소리

    }

    private void OnTriggerEnter(Collider col)
    {
        
            
        if (col.gameObject.tag == "truck")
            
        {            
            if (setting.set)
            {
                HUD_Green.SetActive(false);
                HUD_Red.SetActive(true);
            }
            
        }

        
        if (col.gameObject.tag == "hudoff1")
            
        {
            if (setting.set)
            {
                HUD_Green.SetActive(false);
                HUD_Red.SetActive(false);
            }

            if (!log16Once)
            {
                StartCoroutine(Delay_logMesege("event16 end"));
                log16Once =true;
            }
        }

            
        if (col.gameObject.tag == "hudon1")
            
        {

            if (setting.set)
            {
                HUD_Green.SetActive(true);
                test_sound[0].clip = test_soundClip[0];
                test_sound[0].Play();

            }
            if (!log19Once)
            { 
                Debug.Log("event16 start");
                log19Once =true;
            }
            
        
        }


        if (col.gameObject.tag == "hudoff2")

        {
            if (setting.set)
            {
                HUD_Green.SetActive(false);
                HUD_Red.SetActive(false);
            }

            if (!log17Once)
            {
                StartCoroutine(Delay_logMesege("event17 end")); // 차폭감 주차된 트럭"));
                log17Once =true;
            }
        }
            
        if (col.gameObject.tag == "hudon2")
        
        {

            if (setting.set)
            {
                HUD_Green.SetActive(true);
                test_sound[0].clip = test_soundClip[0];
                test_sound[0].Play();
            }
            if (!log18Once)
            {
                Debug.Log("event17 start"); // 차폭감 주차된 트럭");
                log18Once =true;
            }   
        }
        
    }


    

    private void OnTriggerExit(Collider col)
    {
        if (setting.set == true)
        {
            if (col.gameObject.tag == "truck")
            {
                StartCoroutine(hudGreenOn_off());

            }
        }
    }

    IEnumerator hudGreenOn_off()
    {
        yield return StartCoroutine(hudGreenOn());
        yield return StartCoroutine(hudoff());
    }



    IEnumerator hudGreenOn()
    {
        HUD_Green.SetActive(true);
        HUD_Red.SetActive(false);

        yield return new WaitForSeconds(5f);
    }

    IEnumerator hudoff()
    {
        HUD_Green.SetActive(false);
        HUD_Red.SetActive(false);

        yield return null;
    }

    IEnumerator Delay_logMesege(string log)
    {
        yield return new WaitForSeconds(5f);
        Debug.Log(log);
    }
}
