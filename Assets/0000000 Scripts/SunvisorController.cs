using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SunvisorController : MonoBehaviour
{
    public Transform Sunvisor_off;
    public Transform Sunvisor_on_1;
    public Transform Sunvisor_on_2;
    public string status = "off";
    public float Speed = 0.01f;
    [SerializeField] private Setting_Manager setting;



    // Update is called once per frame
    void Update()
    {
        if (setting.set == true)
        {
            if (status == "on1")
            {
                transform.position = Vector3.MoveTowards(transform.position, Sunvisor_on_1.position, Speed * Time.deltaTime); //본인 위치에서 선바이저 1단계 위치로 간다.}

            }

            else if (status == "on2")
            {
                transform.position = Vector3.MoveTowards(transform.position, Sunvisor_on_2.position, Speed * Time.deltaTime); //본인 위치에서 선바이저 2단계위치로 간다.
            }

            else
            {
                transform.position = Vector3.MoveTowards(transform.position, Sunvisor_off.position, Speed * Time.deltaTime);
            }
        }
       
    }
    public void SunvisorOn_1()
    {
        status = "on1";
    }

    public void SunvisorOn_2()
    {
        status = "on2";
    }

    public void SunvisorOff()
    {
        status = "off";
    }






    
}
