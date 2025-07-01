using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Follow : MonoBehaviour
{
    public Transform vehicleAheadTransform; 
    public Rigidbody rigibody;
    float dist;
    void FixedUpdate()
    {
        dist = (LeadCarStateMachine.Instance.currentState == DistanceState.Collision) ? 80 : 70;

        float dist2 = LeadCarStateMachine.Instance.GetCurrentDistance();
        if (Vector3.Distance(LeadCarStateMachine.Instance.playerCarController.transform.position, transform.position) 
            < 4)
        {
            dist = dist2 + 20;
        } 
        Vector3 targetPos = vehicleAheadTransform.position - new Vector3(0, 0, dist);
        rigibody.MovePosition(Vector3.Lerp(transform.position, targetPos, Time.fixedDeltaTime * 5f));
    }
}
