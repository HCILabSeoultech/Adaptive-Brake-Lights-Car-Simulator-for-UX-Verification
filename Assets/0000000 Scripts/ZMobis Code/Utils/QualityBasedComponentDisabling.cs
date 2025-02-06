using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QualityBasedComponentDisabling : MonoBehaviour
{

    [SerializeField] List<MonoBehaviour> m_components = new List<MonoBehaviour>();
    [SerializeField] List<int> m_qualityLevels = new List<int>();

    // Start is called before the first frame update
    void Start()
    {
        int currentQuality = QualitySettings.GetQualityLevel();
        if (m_qualityLevels.Contains(currentQuality))
        {
            foreach (var comp in m_components.Where(comp => comp != null))
                comp.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
