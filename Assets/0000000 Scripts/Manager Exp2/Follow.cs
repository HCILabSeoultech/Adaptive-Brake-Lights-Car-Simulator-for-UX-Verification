using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Follow : MonoBehaviour
{
    public Transform vehicleAheadTransform; 
    public Rigidbody rigibody;
    void FixedUpdate()
    {
        Vector3 targetPos = vehicleAheadTransform.position - new Vector3(0, 0, 70);
        rigibody.MovePosition(Vector3.Lerp(transform.position, targetPos, Time.fixedDeltaTime * 5f));
    }
}
