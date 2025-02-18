using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarReset : MonoBehaviour
{
    public GameObject car;
    public Rigidbody rb;
    private Vector3 m_initPosition;
    private Quaternion m_initRotation;
    public GameObject traingStart;
    public GameObject TestStart;
    

    private void Start()
    {
        RecordInitialTransform();
        Debug.Log(gameObject.name);
    }

    private void Update()
    {
        float parkInput = Input.GetAxis("Car Reset");
        if (parkInput != 0)
        {
            ResetCar();
        }

        if (Input.GetKeyDown(KeyCode.Alpha0)) // 0번을 누르면 연습주행 시작부분에 안착!!!
        {
            ResetTraingStart();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) // 1번을 누르면 실제 주행 시작부분에 안착!!!
        {
            ResetTestStart();
        }
    }

    public void RecordInitialTransform()
    {
        m_initPosition = car.transform.position;
        m_initRotation = car.transform.rotation; 
    }

    public void ResetCar()
    {
        rb.Sleep();
        car.transform.position = m_initPosition;
        car.transform.rotation = m_initRotation;
        rb.WakeUp();
    }

    public void ResetTraingStart()
    {
        rb.Sleep();
        car.transform.position = traingStart.transform.position;
        car.transform.rotation = traingStart.transform.rotation;
        rb.WakeUp();
    }

    public void ResetTestStart()
    {
        rb.Sleep();
        car.transform.position = TestStart.transform.position;
        car.transform.rotation = TestStart.transform.rotation;
        rb.WakeUp();
    }
}