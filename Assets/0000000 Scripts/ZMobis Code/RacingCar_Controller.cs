using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;

public class RacingCar_Controller : MonoBehaviour
{
    public WheelCollider[] wheels = new WheelCollider[4]; //wheels�� WheelColliderdml �迭�� �����ϰ� �迭�� ũ�⸦ 4�� ����
    public GameObject[] wheelMeshs = new GameObject[4];   //wheelMeshs�� GameObject �迭�� �����ϰ� �迭�� ũ�⸦ 4�� ����
    public float Torque = 0f; // ������ ȸ����ų ��
    public float streeringMaxAngle = 45f; // ������ ȸ������
    public float speedLimit = 50f;
    public float speed;
    public float accel;
    public float back;
    public float handleInput = 0;

    public GameObject RacingCar;
    public GameObject CenterOfMass;



    // 자동차 ai 관련 
    public trackWaypoints_RacingCar waypoints;
    public Transform currentWaypoint;
    public List<Transform> nodes = new List<Transform>();
    public int distanceOffset = 1;
    public float sterrForce = 1f;

    // Start is called before the first frame update
    void Start()
    {
        Torque = 0f;
    }

    private void Awake()
    {
        //자율주행을 위한 루트
        waypoints = GameObject.FindGameObjectWithTag("path3").GetComponent<trackWaypoints_RacingCar>();
        nodes = waypoints.nodes;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        moveCar();
        calculateDistanceOfWaypoints(); 
        AISteer(); // 핸들링 인풋 설정
        steerCar(); // 실제 핸들링 인풋을 받고 움직이는 함수 
        animateWheelMeshs();
    }

    private void OnTriggerEnter(Collider col)
    {
        if(col.gameObject.name == "Race_end")
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

    private void calculateDistanceOfWaypoints()
    {
        Vector3 position = gameObject.transform.position; // 자동차의 위치 
        float shortestDistance = Mathf.Infinity;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 difference = nodes[i].position - position; // 웨이포인트와 자동차의 거리
            float currentDistance = difference.magnitude;
            if (currentDistance < shortestDistance)
            {
                int nextIndex = (i + 1) % nodes.Count; // 다음 웨이포인트의 인덱스 계산
                currentWaypoint = nodes[nextIndex];
                shortestDistance = currentDistance;
            }
        }
    }

    private void AISteer()
    {
        Vector3 relative = transform.InverseTransformPoint(currentWaypoint.transform.position);
        relative /= relative.magnitude;

        handleInput = (relative.x / relative.magnitude) * sterrForce;

    }


    private void OnDrawGizmos()
    {
        //Gizmos.DrawWireSphere(currentWaypoint.position, 3);
    }
    //Ai기능 켜지면 꼭 킬것
}
