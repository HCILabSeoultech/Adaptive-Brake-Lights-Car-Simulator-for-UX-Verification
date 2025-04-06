using com.unity.testtrack.Data;
using com.unity.testtrack.physics;
using com.unity.testtrack.terrainsystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.unity.testtrack.vehicle
{
    public class TireRollPhysicalMaterialResponse : MonoBehaviour
    {
        [SerializeField] AudioSource m_audioSourceTemplate;
        [SerializeField] AudioSource m_audioSourceAccel; 
        [SerializeField] VolvoCars.Data.WheelVelocity m_wheelVelocity = default;
        [Range(0, 1000)]
        [SerializeField] float m_volumeAt0WheelVelocity = 0;
        [Range(0, 1000)]
        [SerializeField] float m_volumeAt100WheelVelocity = 920;

        private void FixedUpdate()
        {
            // Ensure there is an audio clip assigned
            if (m_audioSourceTemplate.clip == null)
            {
                Debug.LogWarning("AudioSource has no clip assigned.");
                return;
            }

            // Calculate the average wheel velocity
            float averageWheelVelocity = GetAverageWheelVelocityValue();

            // Remap the average wheel velocity to a volume level
            float volume = Remap(averageWheelVelocity, 0, 1000, 0, 1);
            m_audioSourceTemplate.volume = Mathf.Clamp(volume, 0, 1.0f);
            if (m_audioSourceAccel != null)
            {
                m_audioSourceAccel.volume = Mathf.Clamp(volume, 0, 1.0f);
            }
        }

        // Function to get the average wheel velocity
        float GetAverageWheelVelocityValue()
        {
            float cumulativeVelocity = m_wheelVelocity.Value.fL;
            cumulativeVelocity += m_wheelVelocity.Value.fR;
            cumulativeVelocity += m_wheelVelocity.Value.rL;
            cumulativeVelocity += m_wheelVelocity.Value.rR;

            return cumulativeVelocity / 4;
        }

        // Remap function to map a value from one range to another
        float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
    }
}

