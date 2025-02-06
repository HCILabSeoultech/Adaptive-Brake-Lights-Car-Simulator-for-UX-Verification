using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpeedTrap : MonoBehaviour
{

    [SerializeField] private VolvoCars.Data.Velocity velocity = default;
    [SerializeField] private bool Speeding = false;

    void OnTriggerStay(Collider col)
    {
        if (col.gameObject.tag == "Speed Trap")
        {
           if(velocity.Value >= 70)
            {
                Speeding = true;
            }
           else Speeding = false;


        }
    }

    private void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.tag == "warnig")
        {
            //경고 소리 및 앰비언트 라이트 재생 
        }
    }

    //private void SpeedOver()
    //{
    //    // if (Speeding == True)
    //    {
    //
    //    앰비언트 라이트 키 send하기 
    //    gui이미지 뛰우기 (단속여부에 따라서 ) 그리고 점수 체크 
    //
    //    }
    //}
}
