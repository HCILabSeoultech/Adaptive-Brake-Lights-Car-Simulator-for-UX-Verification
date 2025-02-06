using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class way_manager : MonoBehaviour
{
    // Start is called before the first frame update
    public bool wayOn_right = true;
    public GameObject way1;
   


    private void OnTriggerStay(Collider col)
    {
        if (col.gameObject == way1)
            wayOn_right = true;

        else wayOn_right = false;
        

    }
}
