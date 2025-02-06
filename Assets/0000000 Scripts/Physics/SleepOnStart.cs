using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.unity.testtrack.physics
{
    [RequireComponent(typeof(Rigidbody))]
    public class SleepOnStart : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            body.Sleep();
        }
    }
}
