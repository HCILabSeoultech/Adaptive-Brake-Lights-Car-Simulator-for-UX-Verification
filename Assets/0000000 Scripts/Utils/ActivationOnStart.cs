using UnityEngine;

public class ActivationOnStart : MonoBehaviour
{
    public bool         m_activate;
    public GameObject   m_target;

    // Start is called before the first frame update
    void Start()
    {
        if (m_target == null)
            m_target = gameObject;

        m_target.SetActive(m_activate);
    }
}
