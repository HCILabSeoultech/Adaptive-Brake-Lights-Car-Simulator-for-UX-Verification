using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrossroadManager_2 : MonoBehaviour
{
    [SerializeField] public GameObject[] lights; // 0 : red, 1 : yellow, 2 : blue
    [SerializeField] public GameObject[] lights_street1; //  0 : red, 1 : Green
    [SerializeField] public GameObject[] lights_street2; //  0 : red, 1 : Green
    [SerializeField] private CrossWalkManager_2[] crosswalks; // 
    [SerializeField] private TabletAudioManager tabletAudioManager;
    [SerializeField] private Setting_Manager setting;
    public SerialController serialController;
    public GameObject turn_right;
    public GameObject signal_check;
    public GameObject leftCar;
    private bool isInitialized = false; // 함수 호출을 한 번만 하도록 제어할 플래그
    private void Awake()
    {
        //InitializeLoad();
        turn_right.SetActive(false);
        signal_check.SetActive(false);
        leftCar.SetActive(false);
    }

    public void InitializeLoad()
    {
        // 플래그를 확인하고, 호출되었으면 다시 호출되지 않도록 막음
        if (isInitialized) return;

        // 호출되었다는 플래그 설정
        isInitialized = true;
        // TODO : 교차로에 접근한 경우(콜라이더 충돌 시)에 이 메서드 호출하기
        // 운전자가 근처에 왔을 때 교차로 상황을 초기화하고 진행합니다.
        //Debug.Log("교차로 상황 초기화, 시작 (20초 후 초록불 신호 켜짐)");
        StartCoroutine(CrossroadRoutine());
    }

    private void TurnLight(int index)
    {
        foreach (var light in lights)
        {
            light.SetActive(false);
        }
        lights[index].SetActive(true);
    }

    private void TurnLight_street1(int index)
    {
        foreach (var light_street1 in lights_street1)
        {
            light_street1.SetActive(false);
        }
        lights_street1[index].SetActive(true);
    }
    private void TurnLight_street2(int index)
    {
        foreach (var light_street2 in lights_street2)
        {
            light_street2.SetActive(false);
        }
        lights_street2[index].SetActive(true);
    }

    private IEnumerator CrossroadRoutine()
    {
        yield return StartCoroutine(Phase1());
        yield return StartCoroutine(Phase2());
        yield return StartCoroutine(Phase3());
    }
    private IEnumerator Phase1()
    {
        // 운전자가 사거리에 도착한 직후, 횡단보도로 사람들이 걸어다닌다.
        crosswalks[0].TriggerPhase1Animation();// 걷는것 
        TurnLight_street2(0); // 우측 횡단보도 신호등 빨간불 (원래도 빨간불임)
        TurnLight_street1(1); // 앞쪽 횡단보도 신호등 초록불
        signal_check.SetActive(true);
        if (setting.set == true)
        {
            //serialController.SendSerialMessage("0"); // 초기화
            serialController.SendSerialMessage("23"); // 중앙개체 인식(사람)

        }
        // 20초 대기
        yield return new WaitForSeconds(20f);
    }
    private IEnumerator Phase2()
    {
        // 운전자의 앞 횡단보도는 빨간불이 되고 이후 운전자의 불의 초록불이 된다 동시에 우회전 횡단보도가 초록불이 된다.
        TurnLight_street1(0); //빨간 불 
        leftCar.SetActive(true); // 왼쪽에서 차량이 지나감
        if (setting.set == true)
        {
            serialController.SendSerialMessage("0"); // 초기화

        }
        yield return new WaitForSeconds(3f);
        if (setting.set == true)
        {
            serialController.SendSerialMessage("0"); // 초기화
            serialController.SendSerialMessage("21"); // 왼쪽 엠비언트 붉은 색 

        }
        yield return new WaitForSeconds(8f); // 모두 빨간색 
        TurnLight(2);
        TurnLight_street2(1); // 우측 횡단보도 초록불 
        signal_check.SetActive(false);
        crosswalks[0].TriggerPhase2Animation();
        if (setting.set == true)
        {
            serialController.SendSerialMessage("0");//초기화
            serialController.SendSerialMessage("22"); // 오른쪽에 사람들 존재!!! 
            tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_우회전불가능);
        }
        yield return new WaitForSeconds(20f);
    }

    private IEnumerator Phase3()
    {
        TurnLight_street2(0);
        if (setting.set == true)
        {
            serialController.SendSerialMessage("0");
            tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_우회전가능);
            turn_right.SetActive(true);
        }
        yield return null;
    }
}
