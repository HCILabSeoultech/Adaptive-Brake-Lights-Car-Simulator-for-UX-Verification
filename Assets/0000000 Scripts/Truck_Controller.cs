using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Truck_Controller : MonoBehaviour
{
    // Start is called before the first frame update
    public WheelCollider[] wheels = new WheelCollider[4]; //wheels�� WheelColliderdml �迭�� �����ϰ� �迭�� ũ�⸦ 4�� ����
    public GameObject[] wheelMeshs = new GameObject[4];   //wheelMeshs�� GameObject �迭�� �����ϰ� �迭�� ũ�⸦ 4�� ����
    public float Torque = 100f; // ������ ȸ����ų ��
    
    public float speedLimit = 200f;
    public float speed;
    
    

    public GameObject CenterOfMass;



    private void Start()
    {
        for (int i = 0; i < 4; i++)
        {


        }
    }


    private void FixedUpdate()
    {
        moveCar();
        //animateWheelMeshs();
        



    }

    private void moveCar()
    {
       

        speed = gameObject.GetComponent<Rigidbody>().velocity.magnitude * 3.6f;
        /*gameObject.GetComponent<Rigidbody>().centerOfMass = CenterOfMass.transform.localPosition;*/
        if (speed <= speedLimit)
        {
            for (int i = 0; i < 4; i++)
            {


                wheels[i].motorTorque = Torque;


            }
        }
    }



    

    //private void animateWheelMeshs()
    //{
    //    Vector3 pos = Vector3.zero;
    //    Quaternion rot = Quaternion.identity;

    //    for (int i = 0; i < 4; i++)
    //    {
    //        wheels[i].GetWorldPose(out pos, out rot);
    //        wheelMeshs[i].transform.position = pos;
    //        wheelMeshs[i].transform.rotation = rot;
    //    }
    //}

    
}
