using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelOffRoadCounter : MonoBehaviour
{
    private static int offRoadCount = 0;
    

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Road"))
        {
            offRoadCount++;
            Debug.Log("도로에서 벗어남: " + offRoadCount);
        }
    }

    
}