using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class event_warnig : MonoBehaviour
{
    AudioSource audioSource;
    [SerializeField] private AudioClip[] clip;
    public GameObject Canvas_Take_over;
    public GameObject AutoPilot;
    public GameObject test1;
    public GameObject test2;
    public GameObject test3;
    public SerialController serialController;
    LogitechGSDK.LogiControllerPropertiesData properties;
    private static int Crash = 0;
    private static int test = 0;


    // Start is called before the first frame update


    void Start()
    {
        
        audioSource = GetComponent<AudioSource>();
        Canvas_Take_over.SetActive(false);
        AutoPilot.SetActive(false);
        test1.SetActive(false);
        test2.SetActive(false);
        test3.SetActive(false);

    }
    void OnTriggerEnter(Collider col)
    {




        // 자율주행 구간 진입시 청각알림 신호
        //for (int i = 1; i <= 4; i++)
        //{
        //    string sectionName = "section" + i + "_in";
        //    if (col.gameObject.name == sectionName)
        //    {
        //        Debug.Log("Section " + i + " 들어옴 알림시작");
        //        ring(2);
        //    }
        //}


        if (col.gameObject.name == "Curve_in")
        {
            Debug.Log("곡선구간 진입");
           

        }

        if (col.gameObject.name == "Curve_out")
        {
            Debug.Log("곡선구간 이탈");


        }


        if (col.gameObject.name == "Start")
        {
            Debug.Log("주행시작");
            ring(6);

        }
        if (col.gameObject.name == "finish_line_sound")
        {
            ring(7);

        }


        if (col.gameObject.name == "section0_in")
        {
            Debug.Log("첫번째 자율주행 변환요구");
            ring(4);
        }

        //사고 다발 구간
        if (col.gameObject.name == "warnig_accident")
        {
            ring(1);
        }

        //속도 재한 구간
        if (col.gameObject.name == "speed_limit")
        {
            ring(0);
        }

        //이벤트 종료시 제어권 전환 요구
        if (col.gameObject.name == "Turn_Auto")
        {
            ring(5);
        }



        // 시청각 모음
        //Task1 일때 활성화
        if (test == 1)
        {

            if (col.gameObject.name == "1_event2_6")
            {

                Debug.Log("1번 이벤트 발생 리드 타임6초 패턴2");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();



                serialController.SendSerialMessage("2");


            }
            if (col.gameObject.name == "1_event0_4")
            {
                Debug.Log("2번 이벤트 발생 리드 타임4초 시각+청각만");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
            }
            if (col.gameObject.name == "1_event1_8")
            {

                Debug.Log("3번 이벤트 발생 리드 타임8초 패턴1");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("1");

            }
            if (col.gameObject.name == "1_event3_4")
            {

                Debug.Log("4번 이벤트 발생 리드 타임4초 패턴3");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("3");

            }
            if (col.gameObject.name == "1_event4_8")
            {

                Debug.Log("5번 이벤트 발생 리드 타임8초 패턴4");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("4");

            }
        }



        //Task2 일 때 활성화

        if (test == 2)
        {
            if (col.gameObject.name == "2_event1_4")
            {

                Debug.Log("1번 이벤트 발생 리드 타임4초 패턴1");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("1");

            }

            if (col.gameObject.name == "2_event0_8")
            {
                Debug.Log("2번 이벤트 발생 리드 타임8초 시각+청각만");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
            }

            if (col.gameObject.name == "2_event3_6")
            {
                Debug.Log("3번 이벤트 발생 리드 타임6초 패턴3");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("3");
            }

            if (col.gameObject.name == "2_event2_8")
            {
                Debug.Log("4번 이벤트 발생 리드 타임8초 패턴2");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("2");
            }

            if (col.gameObject.name == "2_event4_6")
            {

                Debug.Log("5번 이벤트 발생 리드 타임6초 패턴4");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("4");
            }

        }



        //Task3 일때 활성화 
        if (test == 3)
        {
            if (col.gameObject.name == "3_event1_6")
            {

                Debug.Log("1번 이벤트 발생 리드 타임 6초 패턴1");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("1");

            }

            if (col.gameObject.name == "3_event0_6")
            {

                Debug.Log("2번 이벤트 발생 리드 타임 6초 시각 + 청각만");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);

            }

            if (col.gameObject.name == "3_event2_4")
            {

                Debug.Log("3번 이벤트 발생 리드 타임 4초 패턴2");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("2");

            }

            if (col.gameObject.name == "3_event3_8")
            {

                Debug.Log("4번 이벤트 발생 리드 타임 8초 패턴3");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("3");

            }

            if (col.gameObject.name == "3_event4_4")
            {

                Debug.Log("5번 이벤트 발생 리드 타임4초 패턴4");
                ring(3);
                AutoPilot.SetActive(false);
                Canvas_Take_over.SetActive(true);
                serialController.SendSerialMessage("4");

            }

        }



    }

    void OnTriggerExit(Collider col) // 
    {


        if (col.gameObject.name == "finish_line")
        {

            Debug.Log( test+"번 실험종료");
            
        }

        if (col.gameObject.name == "SignCircle_02 (1)" || col.gameObject.name == "SteelBeamBase")
        {
            Crash++;
            Debug.Log(Crash + "충돌 ");
        }

    }

    private void FixedUpdate()
    {
        LogitechGSDK.DIJOYSTATE2ENGINES rec;
        rec = LogitechGSDK.LogiGetStateUnity(0);

        // 자율주행 버튼 눌렸을때 청각알림
        if (rec.rgbButtons[0] == 128 || Input.GetKeyUp(KeyCode.T))
        {
            ring(2);
            AutoPilot.SetActive(true);
        }

        if(Input.GetKeyUp(KeyCode.F1)) 
        {
            test = 1;
            Debug.Log(test + "번 실험");
            test1.SetActive(true);
            test2.SetActive(false);
            test3.SetActive(false);
        }

        if (Input.GetKeyUp(KeyCode.F2))
        {
            test = 2;
            Debug.Log(test + "번 실험");
            test2.SetActive(true);
            test1.SetActive(false);
            test3.SetActive(false);
        }

        if (Input.GetKeyUp(KeyCode.F3))
        {
            test = 3;
            Debug.Log(test + "번 실험");
            test3.SetActive(true);
            test1.SetActive(false);
            test2.SetActive(false);

        }
    }

    private void ring(int n)
    {
        audioSource.clip = clip[n];
        audioSource.Play();
    }
}
