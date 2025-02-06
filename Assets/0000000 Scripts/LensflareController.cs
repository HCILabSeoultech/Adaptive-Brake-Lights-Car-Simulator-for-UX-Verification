using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LensflareController : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F; // 설정할 키
    private LensFlareComponentSRP lensFlare;
    public string status = "off";

    void Start()
    {
        lensFlare = GetComponent<LensFlareComponentSRP>();
        if (lensFlare == null)
        {
            Debug.LogError("LensFlare component not found on " + gameObject.name);
        }
        
    }

    public void flareon()
    {
       status = "on";
    }
    public void flareoff()
    {
        status="off";
    }


    void Update()
    {
        if (status == "on")
        {
            if (lensFlare != null)
            {
                lensFlare.enabled = true;
            }
        }
        if (status == "off") 
        {
            if (lensFlare != null)
            {
                lensFlare.enabled = false;
            }
        }

    }
}
