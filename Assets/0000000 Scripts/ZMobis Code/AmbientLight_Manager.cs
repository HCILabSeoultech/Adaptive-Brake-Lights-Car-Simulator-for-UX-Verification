using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmbientLight_Manager : MonoBehaviour
{
    public SerialController serialController;
    private bool Speeding1 = false;
    private bool Speeding2 = false;
    [SerializeField] private VolvoCars.Data.Velocity velocity = default;
    [SerializeField] private TabletAudioManager tabletAudioManager;
    [SerializeField] private Setting_Manager setting;

    
    public float speed;
    public GameObject Rain;
    public GameObject rainlight;
    public GameObject Sun;
    public GameObject RainCars;
    public AudioSource RainSound;

    private bool overtake_once = false;

    public int Over_count;

    //비내릴때 빛의 intensity 조절

    public float targetIntensity = 0f;  // 목표 밝기
    public float OriginnalIntensity = 5020f; // 원 밝기
    public float duration = 5f;  // 에미션이 변하는 데 걸리는 시간


    // Start is called before the first frame update

    void Start()
    {
        
        Rain.SetActive(false);
        rainlight.SetActive(false);
        RainCars.SetActive(false);
        MuteSound();
        overtake_once = false;
        
        
    }

    public void MuteSound()
    {
        if (RainSound != null)
        {
            RainSound.mute = true;  // 소리 끄기
        }
    }

    // 오디오 소리를 다시 켜는 함수
    public void UnmuteSound()
    {
        if (RainSound != null)
        {
            RainSound.mute = false;  // 소리 켜기
        }
    }

    private void Awake()
    {
       
       
        if (rainlight == null)
        {
            Debug.LogError("빛이 없으랴...");
            Debug.Log(gameObject.name);
        }


    }

    //IEnumerator ChangeLightIntensity(float target, float time)
    //{
    //    float startIntensity = rainlight.intensity;
    //    float elapsedTime = 0f;

    //    while (elapsedTime < time)
    //    {
    //        elapsedTime += Time.deltaTime;
    //        rainlight.intensity = Mathf.Lerp(startIntensity, target, elapsedTime / time);  // 서서히 밝기 변화
    //        yield return null;
    //    }

    //    rainlight.intensity = target;  // 목표 밝기로 설정
    //}
    private void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.name == "Rain_start")
        {
            Rain.SetActive(true);
            rainlight.SetActive(true);
            Sun.SetActive(false);
            UnmuteSound();
            RainCars.SetActive(true);

            //StartCoroutine(ChangeLightIntensity(targetIntensity, duration));
            Debug.Log("event2 start"); // 세찬비");
            if (setting.set)
            {
                
                serialController.SendSerialMessage("8");
                tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_stress_rain);
                //serialController.SendSerialMessage("2");
                
            }
            
        }

        if (col.gameObject.name == "Rain_end")
        {
            Rain.SetActive(false);
            rainlight.SetActive(false);
            Sun.SetActive(true);
            RainCars.SetActive(false);
            MuteSound();
            //tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_neutral,2);

            if (!setting.set)
            {
                StartCoroutine(Delay_logMesege("event2 end", 10));
            }
         }

        
        if (col.gameObject.tag == "warning1")
        {
            Debug.Log("event3 start");
            if (setting.set)// setting이 켜져 있으면 작동
            {
                serialController.SendSerialMessage("1"); //엠비언트 라이트 작동
                tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_과속단속구간_경고); //GUI 서비스 작동
            }
            else
            {
                tabletAudioManager.audiosourceTTS.clip = tabletAudioManager.audioClipTTS[(int)TTSSound.과속_전방에과속단속구간입니다제한속도는];
                tabletAudioManager.audiosourceTTS.Play();

            }
        }

        if (col.gameObject.tag == "warning2")
        {
            Debug.Log("event4 start");// 과속50");
            if (setting.set)// setting이 켜져 있으면 작동
            {
                serialController.SendSerialMessage("1"); //엠비언트 라이트 작동
                tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_과속단속구간_경고); //GUI 서비스 작동
            }
            else
            {
                tabletAudioManager.audiosourceTTS.clip = tabletAudioManager.audioClipTTS[(int)TTSSound.과속_전방에과속단속구간입니다제한속도는];
                tabletAudioManager.audiosourceTTS.Play();

            }
        }

        if (col.gameObject.tag == "normal")
        {
            //tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_과속단속 경고 );
            if (setting.set) tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal);
            
        }

        

        

       
    }

    void OnTriggerStay(Collider col)
    {
        speed = ((int)(3.6f * Mathf.Abs(velocity.Value) + 0.9f));
        if (col.gameObject.name == "Speed Trap1")
        {
            if (speed >= 70)
            {
                Speeding1 = true;
            }           
        }

        if (col.gameObject.name == "Speed Trap2")
        {
            if (speed >= 70)
            {
                Speeding2 = true;
            }
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.gameObject.name == "Speed Trap1")
        {
            if (Speeding1 == true)
            {
                if(setting.set)
                {
                    serialController.SendSerialMessage("25"); 
                    serialController.SendSerialMessage("2");
                    tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_과속단속구간_단속됨);
                }
                Over_count++;
               
            }
            else 
            {
                if (setting.set)
                {
                    serialController.SendSerialMessage("26");
                    serialController.SendSerialMessage("2");
                    tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_과속단속구간_정상통과);
                }
                
            }
            StartCoroutine(Delay_logMesege("event3 end")); // 과속 100")); 
        }

        if (col.gameObject.name == "Speed Trap2")
        {
            if (Speeding2 == true)
            {
                if (setting.set)
                {
                    serialController.SendSerialMessage("25");
                    serialController.SendSerialMessage("2");
                    tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_과속단속구간_단속됨);
                }
                Over_count++;
            }
            else
            {
                if (setting.set)
                {
                    serialController.SendSerialMessage("26");
                    serialController.SendSerialMessage("2");
                    tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_과속단속구간_정상통과);
                }
                
            }
            StartCoroutine(Delay_logMesege("event4 end"));// 과속 50"));
        }

      
        
            if (col.gameObject.name == "overtake" && !overtake_once)
            {
                if (setting.set) tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_neutral,5);
                //Debug.Log("event5 end"); // 앞느린차");
                overtake_once = true;
                if(!setting.set) { Debug.Log("event5 end"); }
            }

        
    }

    IEnumerator Delay_logMesege(string log, float delaytime = 10) 
    {
        yield return new WaitForSeconds(delaytime);
        Debug.Log(log);
    }

   

}
