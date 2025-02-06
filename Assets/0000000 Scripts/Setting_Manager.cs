using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Controller;

public class Setting_Manager : MonoBehaviour
{
    // Start is called before the first frame update
    internal enum Test
    {
        Support_On,
        Support_Off
    }

    public bool set = true;

    [SerializeField] Test Setting;
    [SerializeField] Timer Timer;
    

    public void Update()
    {
        switch (Setting)
        {
            case Test.Support_On:
                set = true;
                
                break;
            case Test.Support_Off:
                set = false;
                break ;
            
        }
    }

}
