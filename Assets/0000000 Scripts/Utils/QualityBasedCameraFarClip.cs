using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QualityBasedCameraFarClip : MonoBehaviour
{
    [Tooltip("Far clipping distance to use for each quality levels")]
    public List<int> m_distances = new List<int>();
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        int currentLevel = QualitySettings.GetQualityLevel();
        if (currentLevel < m_distances.Count)
            Camera.main.farClipPlane = m_distances[currentLevel];
    }
}
