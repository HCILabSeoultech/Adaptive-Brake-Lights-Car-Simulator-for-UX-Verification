using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrossCheckManager : MonoBehaviour
{
    // Start is called before the first frame update
    
    public SerialController serialController;
    [SerializeField] private TabletAudioManager tabletAudioManager;
    int crossed = 0;
   


    private void Update()
    {
        if(crossed == 3) 
        {
            serialController.SendSerialMessage("23");
            tabletAudioManager.ActiveTabletGUI(ImageType.Navigation_normal_우회전가능);
            Debug.Log("사람들 다건넘");
            crossed = 0;
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.gameObject.tag == "Human")
        {
            crossed++;
            Debug.Log("사람한명 건너감");
        }

        


    }
}
