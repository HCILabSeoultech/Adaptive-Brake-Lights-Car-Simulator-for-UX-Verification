using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VolvoCars.Data;

public class LensFlareToggler : MonoBehaviour
{
    public CameraIsInsideCar m_cameraIsInsideCar;
    public LensFlareComponentSRP m_lensFlare;

    // Start is called before the first frame update
    void Start()
    {
        m_cameraIsInsideCar.Subscribe(cameraStateChange);
    }

    private void OnDestroy()
    {
        m_cameraIsInsideCar.Unsubscribe(cameraStateChange);
    }

    public void cameraStateChange(bool isInside)
    {
        if (m_lensFlare == null)
            return;

        m_lensFlare.allowOffScreen = !isInside;
    }
}
