using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using System.Text;
using System;
using System.IO;
using UnityEngine.UI;

public class SCR_Z_score_Manager : MonoBehaviour
{
    TcpClient client;
    string serverIP = "127.0.0.1";
    int port = 8000;
    byte[] receivedBuffer;
    bool socketReady = false;
    NetworkStream stream;
    private string msg;
    public float SCR_Z_SCORE;
    public float GSR_Z_SCORE;

    
    [SerializeField] private GameObject Ui_stable, Ui_unstable;
    [SerializeField] private GameObject[] z_score_level;
    public string status;

    void Start()
    {
        status = "stable"; // 기본 상태
        
        CheckReceive();
        DeActiveUI();
    }

    void Update()
    {
        if (socketReady && stream.DataAvailable)
        {
            int bytesRead = stream.Read(receivedBuffer, 0, receivedBuffer.Length);
            if (bytesRead > 0)
            {
                msg = Encoding.UTF8.GetString(receivedBuffer, 0, bytesRead).Trim();


                string[] values = msg.Split(',');
                if (values.Length == 2)
                {

                    SCR_Z_SCORE = float.Parse(values[0]); // float로 변환
                    GSR_Z_SCORE = float.Parse(values[1]); // float로 변환 및 분리
                }
            }

            
        }

        
           

            
        
        else if (string.IsNullOrEmpty(msg))
        {
            Debug.Log("파이썬에서 값을 읽지 못했습니다.");
        }
    }

    void CheckReceive()
    {
        if (socketReady) return;
        try
        {
            client = new TcpClient(serverIP, port);

            if (client.Connected)
            {
                stream = client.GetStream();
                receivedBuffer = new byte[client.ReceiveBufferSize];
                Debug.Log("Connect Success");
                socketReady = true;
            }
        }
        catch (Exception e)
        {
            Debug.Log("On client connect exception " + e);
        }
    }

    void OnApplicationQuit()
    {
        CloseSocket();
    }

    void CloseSocket()
    {
        if (!socketReady) return;

        stream?.Close();
        client?.Close();
        socketReady = false;
    }

   

    void DeActiveUI()
    {
        foreach (GameObject z_score in z_score_level)
        {
            z_score.gameObject.SetActive(false);
        }
    }





}
