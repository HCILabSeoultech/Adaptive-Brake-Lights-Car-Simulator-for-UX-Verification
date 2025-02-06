using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ActivationToggler : MonoBehaviour
{
    public GameObject   m_object;
    public KeyCode      m_toggleKey;
    public bool         m_anyKey;

    // Update is called once per frame
    void Update()
    {
        if (m_object == null)
            return;

        var gamepadButtonPressed = false;
        try
        {
            if (m_anyKey)
                gamepadButtonPressed = Gamepad.current.allControls.Any(x => x is ButtonControl button && x.IsPressed() && !x.synthetic);
        }
        catch{ }

        if (m_anyKey && Input.anyKeyDown || gamepadButtonPressed)
            m_object.SetActive(!m_object.activeSelf);
        else if (Input.GetKeyDown(m_toggleKey))
            m_object.SetActive(!m_object.activeSelf);
    }
}
