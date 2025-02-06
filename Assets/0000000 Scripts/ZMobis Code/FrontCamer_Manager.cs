using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrontCamer_Manager : MonoBehaviour
{
    public GameObject Front_Camera;
    public GameObject Under_Camera;
    public GameObject GUI_Camera;
    public GameObject GUI_canvers;
    [SerializeField] private Setting_Manager setting;
    // Start is called before the first frame update
    void Start()
    {
        Front_Camera.SetActive(false);
        Under_Camera.SetActive(false);
    }

    private void OnTriggerEnter(Collider col)
    {
        if (setting.set)
        {
            if (col.gameObject.name == "Camera_front_On")
            {
                GUI_Camera.SetActive(false);
                GUI_canvers.SetActive(false);
                Front_Camera.SetActive(true);
            }

            if (col.gameObject.name == "Camera_under_On")
            {
                Front_Camera.SetActive(false);
                StartCoroutine(UnderviweOn());
            }
        }
        
    }

    IEnumerator UnderviweOn()
    {
        Under_Camera.SetActive(true);
        yield return new WaitForSeconds(3f);//언덕 넘어갈때 3초 정도 보여줌
        GUI_Camera.SetActive(true);
        GUI_canvers.SetActive(true);
        Under_Camera.SetActive(false);

    }
}
