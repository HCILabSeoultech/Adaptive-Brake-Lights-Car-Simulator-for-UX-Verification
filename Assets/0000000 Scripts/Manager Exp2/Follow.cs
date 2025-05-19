using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Follow : MonoBehaviour
{
    public Transform vehicleAheadTransform; 
    void Update()
    {
        transform.position = vehicleAheadTransform.position - new Vector3(0,0,70);
    }
}
