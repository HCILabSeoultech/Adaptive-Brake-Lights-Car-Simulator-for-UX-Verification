using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Racing_Manager : MonoBehaviour
{
    public WheelCollider[] wheels = new WheelCollider[4]; //wheels�� WheelColliderdml �迭�� �����ϰ� �迭�� ũ�⸦ 4�� ����
    public GameObject[] wheelMeshs = new GameObject[4];   //wheelMeshs�� GameObject �迭�� �����ϰ� �迭�� ũ�⸦ 4�� ����
    public float Torque = 4000f; // ������ ȸ����ų ��
    public float streeringMaxAngle = 45f; // ������ ȸ������
    public float speedLimit = 50f;
    public float speed;
    public float accel;
    public float back;
    public float handleInput = 0;

    public int status = 0;

    public GameObject RacingCar;
    public GameObject CenterOfMass;

    // 백미러 빨간불 파란불 제어 
    [SerializeField] private Event_Controller event_Controller;
    [SerializeField] private Setting_Manager setting;

    //빵빵 거림 
    [SerializeField] private AudioSource audiosourceTTS;
    [SerializeField] private AudioClip[] audioClipTTS;

    //우측으로 피해라 
    [SerializeField] private AudioSource audiosourceEffectSound;
    [SerializeField] private AudioClip[] audioClipEffectSound;

    //자동차 Ai 관련
    public trackWaypoints_OtherCars waypoints1;
    public trackWaypoints_Ambulance waypoints2;
    public trackWaypoints_RacingCar waypoints3;
    public Transform currentWaypoint1;
    public Transform currentWaypoint2;
    public Transform currentWaypoint3;
    public List<Transform> nodes1 = new List<Transform>();
    public List<Transform> nodes2 = new List<Transform>();
    public List<Transform> nodes3 = new List<Transform>();
    public int distanceOffset = 1;
    public float sterrForce = 1f;

    //way 차가 오른쪽 차선인지 아닌지 판단
    [SerializeField] private way_manager way;

    // Start is called before the first frame update
    void Start()
    {
        Torque = 1000f;
    }

    private void Awake()
    {
        //자율주행을 위한 루트
        waypoints1 = GameObject.FindGameObjectWithTag("path2").GetComponent<trackWaypoints_OtherCars>(); //우측
        waypoints2 = GameObject.FindGameObjectWithTag("path4").GetComponent<trackWaypoints_Ambulance>(); //좌측
        waypoints3 = GameObject.FindGameObjectWithTag("path3").GetComponent<trackWaypoints_RacingCar>(); // 끝나고 따라갈것 
        nodes1 = waypoints1.nodes;
        nodes2 = waypoints2.nodes;
        nodes3 = waypoints3.nodes;
      
    }

    private void OnEnable()
    {
        if (setting.set) event_Controller.RedOn();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        calculateDistanceOfWaypoints1();
        calculateDistanceOfWaypoints2();
        animateWheelMeshs();
        moveCar();
        steerCar();
        AISteer(status);
    }

    private void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.name == "distance_ambulance" && way.wayOn_right) // 오른쪽차선에 플레이어가 있으니 왼쪽으로 추월 시작 
        {
            //status = 2;
            //Debug.Log("추월시작");
        }
        //if (col.gameObject.name == "distance_ambulance" && !way.wayOn_right) // 왼쪽 차선에 플레이어가 있으니 오른쪽으로 추월 시작 
        //{
        //    //status = 1;
        //    Debug.Log("추월시작");
        //}

        if (col.gameObject.name == "show_ambulance" && way.wayOn_right) // 플레이어가 오른쪽 차선에 있으니 오른쪽 차선으로 추격 시작 
        {
            //status = 1;
            if (setting.set == true)
            {
                event_Controller.RedOn_race();//빨간 불 켜짐 
                //Debug.Log("빨간불켜짐");
            }
            else
            {
                audiosourceEffectSound.clip = audioClipEffectSound[0];
                audiosourceEffectSound.Play(); // 우측으로 피해주세여
            }
        }
        //if (col.gameObject.name == "show_ambulance" && !way.wayOn_right) // 플레이어가 왼쪽 차선에 있으니 왼쪽 차선으로 추격 시작 
        //{
        //    //status = 2;
        //    if (setting.set == true)
        //    {
        //        event_Controller.RedOn_race();//빨간 불 켜짐 
        //        Debug.Log("빨간불켜짐");
        //    }
        //}

        if (col.gameObject.name == "overtaking_ambulance")
        {
            if (setting.set == true)
            {
                event_Controller.GreenOn();// 초록불 켜짐 
                //Debug.Log("초록불 켜짐 ");
            }
            Debug.Log("event14 end");
        }

        if (col.gameObject.name == "test4_end")
        {
            RacingCar.SetActive(false);
        }
    }
    private void moveCar()
    {



        speed = gameObject.GetComponent<Rigidbody>().velocity.magnitude * 3.6f;
        gameObject.GetComponent<Rigidbody>().centerOfMass = CenterOfMass.transform.localPosition;

        if (speed <= speedLimit)
        {
            for (int i = 0; i < 4; i++)
            {


                wheels[i].motorTorque = Torque;


            }
        }
    }


    private void OnTriggerStay(Collider col)
    {
        if (col.gameObject.tag == "braking")
        {
            audiosourceTTS.clip = audioClipTTS[0];
            audiosourceTTS.Play();
            for (int i = 0; i < 4; i++)
            {


                wheels[i].brakeTorque = 1000000f;

            }
        }
        else for (int i = 0; i < 4; i++)
            {


                wheels[i].brakeTorque = 0f;

            }
    }




    private void animateWheelMeshs() // 휠 메쉬 보이는 거 조절 
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        for (int i = 0; i < 4; i++)
        {
            wheels[i].GetWorldPose(out pos, out rot);
            wheelMeshs[i].transform.position = pos;
            wheelMeshs[i].transform.rotation = rot;
        }
    }

    private void steerCar()
    {
        for (int i = 0; i < 2; i++)
        {
            wheels[i].steerAngle = streeringMaxAngle * handleInput;
        }
    }

    private void AISteer(int way)
    {
        Transform currentWaypoint;

        if (way == 1)
        {
            currentWaypoint = currentWaypoint1;
        }
        else if (way == 2)
        {
            currentWaypoint = currentWaypoint2;
        }
        else if (way == 3)
        {
            currentWaypoint = currentWaypoint3;


        }
        else
        {
            //Debug.LogWarning("Invalid type specified for AISteer");
            return;
        }

        Vector3 relative = transform.InverseTransformPoint(currentWaypoint.transform.position);
        relative /= relative.magnitude;

        handleInput = (relative.x / relative.magnitude) * sterrForce;
        //Debug.Log("핸들조정중");
    }

    private void calculateDistanceOfWaypoints1()
    {
        Vector3 position = gameObject.transform.position; // 자동차의 위치 
        float shortestDistance1 = Mathf.Infinity;

        for (int i = 0; i < nodes1.Count; i++)
        {
            Vector3 difference = nodes1[i].position - position; // 웨이포인트와 자동차의 거리
            float currentDistance = difference.magnitude;
            if (currentDistance < shortestDistance1)
            {
                int nextIndex = (i + 1) % nodes1.Count; // 다음 웨이포인트의 인덱스 계산
                currentWaypoint1 = nodes1[nextIndex];
                shortestDistance1 = currentDistance;
            }
        }
    }

    private void calculateDistanceOfWaypoints2()
    {
        Vector3 position = gameObject.transform.position; // 자동차의 위치 
        float shortestDistance2 = Mathf.Infinity;

        for (int i = 0; i < nodes2.Count; i++)
        {
            Vector3 difference = nodes2[i].position - position; // 웨이포인트와 자동차의 거리
            float currentDistance = difference.magnitude;
            if (currentDistance < shortestDistance2)
            {
                int nextIndex = (i + 1) % nodes2.Count; // 다음 웨이포인트의 인덱스 계산
                currentWaypoint2 = nodes2[nextIndex];
                shortestDistance2 = currentDistance;
            }
        }
    }

    private void OnDrawGizmos()
    {
        //Gizmos.DrawWireSphere(currentWaypoint1.position, 3);
        //Gizmos.DrawWireSphere(currentWaypoint2.position, 3);
        //Gizmos.DrawWireSphere(currentWaypoint3.position, 3);
    }

    IEnumerator Delay_logMesege(string log, float delaytime = 3f)
    {
        yield return new WaitForSeconds(delaytime);
        Debug.Log(log);
    }
}
