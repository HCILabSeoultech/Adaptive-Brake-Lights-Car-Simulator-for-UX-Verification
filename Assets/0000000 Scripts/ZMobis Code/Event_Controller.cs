using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Event_Controller : MonoBehaviour
{
    //해피캄 관련 오브젝트
    public GameObject Car1;
    public GameObject Car2;
    public GameObject Car3;
    public GameObject Tire;

    //SCR 통신 관련 스크립트
    [SerializeField] private SCR_Z_score_Manager SCR_Z_Score;

    //HUD && 추월관련 오브젝트
    public GameObject Ambulance;
    public GameObject Truck;
    public GameObject Backmirror_green;
    public GameObject Backmirror_red;
    public GameObject AmbulanceSystem;
    public GameObject Racing;
    public GameObject leftCar;

    int status1 = 0;
    int status2 = 0;

    //운전자 인풋관련 
    [SerializeField] private Controller controller;
    
    private bool auanger = false;

    // 선바이저 및 GUI 스크립트  
    [SerializeField] private LensflareController[] lensflarecontroller;
    [SerializeField] private SunvisorController[] sunvisorcontroller;
    [SerializeField] private TabletAudioManager tabletAudioManager;
    [SerializeField] private RacingCar_Controller RacingCar;
    // 실험종료 사운드 
    [SerializeField] private AudioSource[] test_sound;
    [SerializeField] private AudioClip[] test_soundClip;



    //엠비언트 라이트 및 세팅 
    public SerialController serialController;
    [SerializeField] private Setting_Manager setting;
    public GameObject turn_right;

    //우회전 시나리오 
    [SerializeField] private CrossroadManager crossroadManager;
    [SerializeField] private CrossroadManager_2 crossroadManager2;

    //해피캄 관련 이벤트 시작했는지 체크하는 부울 ... 지나가면 켜지도록 
    private bool strees = false;
    private bool suprise = false;
    private bool anger = false;
    
    

    // Start is called before the first frame update
    void Start()
    {
        strees = false;
        suprise = false;
        anger = false;
        
        Car1.SetActive(false);
        Car2.SetActive(false);
        Car3.SetActive(false);
        Tire.SetActive(false);
        Racing.SetActive(false);
        leftCar.SetActive(false);

        
        Ambulance.SetActive(false);
        AmbulanceSystem.SetActive(false); // 앰뷸런스 콜라이더 관련 
        Truck.SetActive(false);
        Backmirror_green.SetActive(false);
        Backmirror_red.SetActive(false);
        status1 = 0;
        status2 = 0;
        test_sound[0] = GetComponent<AudioSource>(); // 실험 종료 소리
        test_sound[1] = GetComponent<AudioSource>(); // 우회전 안내소리
    }

    // Update is called once per frame
    void OnTriggerEnter(Collider col)
    {
        // 슬프미 시나리오 당분간 사용하지 않음!!!!

        //if (col.gameObject.name == "event1start")
        //{
        //    //Debug.Log("event1 start");
        //    if (setting.set)
        //    {

        //        tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_sad, 1);

        //    }
        //    else
        //    {
        //        StartCoroutine(Delay_logMesege("event1 end", 40));
                
        //    }
        //}

        if (col.gameObject.name == "test1_end")
        {

            Car1.SetActive(true);
            Car2.SetActive(true);
            Debug.Log("event5 start"); // 앞느린차");
            strees = true;
            //if (setting.set == true)
            //{
            //    serialController.SendSerialMessage("8");
            //    tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_stress);
            //    //serialController.SendSerialMessage("2");
            //}

        }

        

        #region test2
        //test2 시나리오 좌절 스트레스 1번 
        

        
        if (col.gameObject.name == "test2_stress_end")
        {
            Car1.SetActive(false);
            Car2.SetActive(false);
            
            
        }
        
        //test2 시나리오 놀람 타이어 등장!
        if (col.gameObject.name == "test2_suprised_Start")
        {
            Tire.SetActive(true);
            Debug.Log("event6 start"); // 타이어");
            
            suprise = true;
        }

        if(col.gameObject.name == "test2_afraid_start")
        {
            Car3.SetActive(true);
        }

        if (col.gameObject.tag == "afraid")
        {
            Debug.Log("event7 start"); // 끼어들기");
            if (setting.set && anger == false)
            {
                anger = true;
            //serialController.SendSerialMessage("7");
            //tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_anger);
            //serialController.SendSerialMessage("2");
            }
            if(!setting.set && anger == false)
            {
               
                anger = true;
            }
        }



        #endregion

        #region turn_right
        //우회전 시나리오 스크립트 호출 
        if (col.gameObject.name == "trigger_turnright")
        {
            Debug.Log("event8 start"); // 우회전1");
            
            if (crossroadManager != null)
            {

                
                crossroadManager.InitializeLoad();
                
               
                
                
            }
            else
            {
                Debug.LogError("CrossroadManager를 찾을 수 없습니다.");
            }
        }
           
            

        //우회전 시나리오 스크립트 호출 2
        if (col.gameObject.name == "trigger_turnright2")
        {
            Debug.Log("event12 start"); // 우회전2");
            if (crossroadManager2 != null)
            {
                if (status2 == 0)
                {
                    crossroadManager2.InitializeLoad();
                    
                }

            }
            else
            {
                Debug.LogError("CrossroadManager_2를 찾을 수 없습니다.");
            }
        }

        if(col.gameObject.name == "test3_end")
        {
            Debug.Log("event12 end"); // 우회전2"));
        }

        #endregion

        #region test3 Sunvisor
        //test3 선바이저 시나리오 
        //햇빛이 비춤 
        if (col.gameObject.name == "test3_start")
        {
            turn_right.SetActive(false);
            Debug.Log("event9 start"); // 터널전");
            StartCoroutine(Delay_logMesege("event8 end"));// 우회전1"));
            

        }

        if (col.gameObject.name == "test3_end")
        {
            turn_right.SetActive(false);
           


        }
        if (col.gameObject.name == "Enter_tunnel") StartCoroutine(Delay_logMesege("event9 end"));// 터널전"));
        if (col.gameObject.name == "Exit_tunnel")
        {
            Debug.Log("event10 start"); // 터널후");
            StartCoroutine(Delay_logMesege("event10 end")); // 터널후 "));
        }

        if(col.gameObject.name == "Clime") Debug.Log("event11 start"); // 오르막길");
        if (col.gameObject.name == "Clime_end") Debug.Log("event11 end");//  오르막길");

        if (col.gameObject.tag == "flare")
        {


            if (lensflarecontroller != null)
            {
                lensflarecontroller[0].flareon();
                //Debug.Log("햇빛때문에 눈부셔!!");
            }
            else;//Debug.Log("렌즈플레어 어디감???");
        }

        //햇빛이 비치지 않음 터널 진입시, 선바이저 시나리오 끝나는 경우
        if (col.gameObject.name == "flare_off")
        {
            if (lensflarecontroller != null)
            {
                lensflarecontroller[0].flareoff();
            }
            if (sunvisorcontroller != null)
            {
                if (setting.set == true)
                {
                    sunvisorcontroller[0].SunvisorOff();
                    tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal);
                    //Debug.Log("선바이져 올라감");
                }
            }
        }
        
        
        
        if (col.gameObject.name == "sunvisor_on1") //1단계 처음 우회전하고 나서 보게되는 햇빛 && 터널 통과시 나오는 햇빛 가려줌
        {
            

            if (sunvisorcontroller != null)
            {
                if (setting.set)
                {
                    sunvisorcontroller[0].SunvisorOn_1();
                    tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_선바이저알림_강한햇살);
                    //Debug.Log("선바이져 내려왔어!!");
                }
            }
            if (lensflarecontroller != null)
            {
                if (setting.set) lensflarecontroller[0].flareoff();
                //Debug.Log("눈부심효과 끝남");
            }
            
        }

        if (col.gameObject.name == "sunvisor_on2") //2단계 오르막길 진입후 보게되는 햇빛 가려줌  
        {


            if (sunvisorcontroller != null)
            {
                if (setting.set == true)
                {
                    sunvisorcontroller[0].SunvisorOn_2();
                    tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_선바이저알림_강한햇살);
                }
            }
            if (lensflarecontroller != null)
            {
                if (setting.set) lensflarecontroller[0].flareoff();
            }

        }


        if (col.gameObject.name == "sunvisor_off") //내리막길이거나 시라리오 끝났을때 꺼짐  
        {


            if (sunvisorcontroller != null)
            {
                sunvisorcontroller[0].SunvisorOff();
            }
            if (lensflarecontroller != null)
            {
                lensflarecontroller[0].flareoff();
            }

        }

        #endregion
        #region test4 ambulance
        if (col.gameObject.name == "test4_start")
        {
            Ambulance.SetActive(true); //앰뷸런스 등장!
            AmbulanceSystem.SetActive(true); // 관련 시스템 , 백미러에 앰뷸런스가 보일때 판단하는 거리오브젝터 생성, 추월 콜라이더 생성 등등
            Debug.Log("event13 start"); // 구급차 추월");
        }

        if (col.gameObject.name == "ambulance_end")
        {
            Racing.SetActive(true); //레이싱카 등장!
            AmbulanceSystem.SetActive(true); // 관련 시스템 , 백미러에 앰뷸런스가 보일때 판단하는 거리오브젝터 생성, 추월 콜라이더 생성 등등
            Debug.Log("event14 start");// 스포츠카 추월");
        }





        #endregion
        if (col.gameObject.name == "Race")
        {
            Debug.Log("event15 start");// 차선축소 끼어들기");
            RacingCar.Torque = 4000f; // 차가 움직이기 시작
            if (setting.set)
            {
                serialController.SendSerialMessage("7");
                tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_anger2);
                //serialController.SendSerialMessage("2");
            }
            if(!setting.set)
            {
                StartCoroutine(Delay_logMesege("event15 end", 40));
            }
        }

        #region test5 truck HUDtest
        if (col.gameObject.name == "test5_start")
        {
            Truck.SetActive(true); //트럭생성
            //Debug.Log("event16 start"); // 차폭감 트럭");
        }

        #endregion
        #region 실험안내 사운드 
        if (col.gameObject.name == "test5_parking_end")
        {
           test_sound[0].clip = test_soundClip[0]; //실험 종료 사운드
           test_sound[0].Play();
           Debug.Log("the end");
                
        }

        if(col.gameObject.tag == "turnright_sound")
        {
            test_sound[1].clip = test_soundClip[1]; // 우회전 안내 사운드
            test_sound[1].Play();

        }

        if(col.gameObject.tag == "stay1way")
        {
            
                test_sound[2].clip = test_soundClip[2]; // 1차선으로 가라는 사운드
                test_sound[2].Play();
            
        }

        if (col.gameObject.name == "1waywarnig")
        {
            test_sound[3].clip = test_soundClip[3];  // 도로폭이 좁아지니 안내하는 사운드
            test_sound[3].Play();

        }



        #endregion
    }

    private void OnTriggerStay(Collider col)
    {
        //stress 발생 앞차 느리게 감  구간안에서 scr_z score가 3을 넘으면 발동!!
        if (col.gameObject.name == "stress_space" && SCR_Z_Score.SCR_Z_SCORE >= 3 && (strees) /*&& (controller.parkInput > 0.2 || Mathf.Abs(controller.rawSteeringInput) > 0.015 )*/) // 조건 구간안에 있고 이벤트 시작 구간 지났고 브레이크를 일정이상 밟거나 스티어링 휠을 어느정도 돌리면 발동!!
        {
            if (setting.set)
            {
                serialController.SendSerialMessage("8");
                tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_stress);
                strees = false;
            }

            if (!setting.set)
            {
                
                strees = false;
            }
        }
        // 놀람 이벤트 구간안에서 scr_z score가 3을 넘으면 발동!!
        if (col.gameObject.name == "suprise_space" && SCR_Z_Score.SCR_Z_SCORE >= 3 && (suprise) /* && (controller.parkInput > 0.2 || Mathf.Abs(controller.rawSteeringInput) > 0.015)*/) // 조건 구간안에 있고 이벤트 시작 구간 지났고 브레이크를 일정이상 밟거나 스티어링 휠을 어느정도 돌리면 발동!!
        {
            if (setting.set)
            {
                serialController.SendSerialMessage("8");
                tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_surprise);
                suprise = false;
            }
            if (!setting.set)
            {
                StartCoroutine(Delay_logMesege("event6 end", 40));
                suprise = false;
            }
        }
        // 화남 이벤트 구간안에서 scr_z score가 3을 넘으면 발동!!
        if (col.gameObject.name == "anger_space" && SCR_Z_Score.SCR_Z_SCORE >= 3 && (anger) /*&& (controller.parkInput > 0.1 || Mathf.Abs(controller.rawSteeringInput) > 0.01)*/) // 조건 구간안에 있고 이벤트 시작 구간 지났고 브레이크를 일정이상 밟거나 스티어링 휠을 어느정도 돌리면 발동!!
        {
            if (setting.set == true)
            {
                serialController.SendSerialMessage("7");
                tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_anger1);
                anger = false;
            }

            if (!setting.set)
            {
                StartCoroutine(Delay_logMesege("event7 end", 40));
                anger = false;
            }
        }


    }

   

    public void GreenOn()
    {

        StartCoroutine(Green_on());
    }

    private IEnumerator Green_on()
    {
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(green());
        yield return StartCoroutine(turnOff());
        tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal);
    }

    public void RedOn()
    {
       
        Backmirror_green.SetActive(false);
        Backmirror_red.SetActive(true);
        tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_warning_구급차추월);
    }

    public void RedOn_race()
    {

        Backmirror_green.SetActive(false);
        Backmirror_red.SetActive(true);
        tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_warning_후방차추월);
    }

    private IEnumerator green()
    {
        // 5초동안 초로빛이 난다. 
        Backmirror_red.SetActive(false);
        Backmirror_green.SetActive(true);
        

        // 5초 대기
        yield return new WaitForSeconds(5f);
    }

   

    private IEnumerator turnOff()
    {
        // 백미러 불빛이 모두 꺼짐 
        Backmirror_red.SetActive(false);
        Backmirror_green.SetActive(false);

        
        yield return null;
    }

    IEnumerator Delay_logMesege(string log, float delaytime = 10f)
    {
        yield return new WaitForSeconds(delaytime);
        Debug.Log(log);
    }


}
