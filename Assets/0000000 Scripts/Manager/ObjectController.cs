using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectController : MonoBehaviour
{
    [SerializeField] private Transform enviroment1;
    [SerializeField] private Transform enviroment2;

    List<float> triggerTime = new List<float>();

    public void MoveEnviroment1()
    {
        enviroment1.position += new Vector3(0f, 0f, 4000f);
        //Debug.Log("Move 1");
    }

    public void MoveEnviroment2()
    {
        enviroment2.position += new Vector3(0f, 0f, 4000f);
        //Debug.Log("Move 2");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("half"))
        {
            if (triggerTime.Count != 0)
            {
                if (other.gameObject.name == "half1")
                {
                    if (Time.time - (triggerTime[triggerTime.Count - 1]) < 10f) return; // �ֱ� Ʈ���Ű�
                    MoveEnviroment2();
                }
                else if (other.gameObject.name == "half2")
                {
                    if (Time.time - (triggerTime[triggerTime.Count - 1]) < 10f) return;
                    MoveEnviroment1();
                }
                triggerTime.Add(Time.time);
            }
            else
            {
                if (other.gameObject.name == "half1")
                {                 
                    MoveEnviroment2();
                }
                else if (other.gameObject.name == "half2")
                {                    
                    MoveEnviroment1();
                }
                triggerTime.Add(Time.time);
            }
        }
    }
}