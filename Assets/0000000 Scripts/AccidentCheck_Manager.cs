using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AccidentCheck_Manager : MonoBehaviour
{
    int accidentCount = 0;
    [SerializeField] AmbientLight_Manager AmbientLight_Manager;
    // Start is called before the first frame update
    private void Start()
    {
        accidentCount = 0;
    }


    void OnCollisionEnter(Collision col)
    {
        
        accidentCount++;
        Debug.Log("Accident!!");
           
       
  

        

    }

    void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.tag == "signal")
        {
            accidentCount++;
            Debug.Log("Traffic signal ignore");
        }
    }



    private void OnDisable()
    {
        Debug.Log("breakingRules: "+(accidentCount+AmbientLight_Manager.Over_count));
    }
}
