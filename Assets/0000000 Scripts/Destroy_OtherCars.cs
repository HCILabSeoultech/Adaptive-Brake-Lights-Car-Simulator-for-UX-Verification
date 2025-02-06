using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Destroy_OtherCars : MonoBehaviour
{
    // Start is called before the first frame update
    // 이 함수는 트리거가 발생했을 때 호출됩니다.
    private void OnTriggerEnter(Collider other)
    {
        // other 오브젝트가 'OtherCars' 태그를 가지고 있는지 확인합니다.
        if (other.CompareTag("OtherCars"))
        {
            // 해당 오브젝트를 씬에서 삭제합니다.
           other.gameObject.SetActive(false);
        }
    }
}
