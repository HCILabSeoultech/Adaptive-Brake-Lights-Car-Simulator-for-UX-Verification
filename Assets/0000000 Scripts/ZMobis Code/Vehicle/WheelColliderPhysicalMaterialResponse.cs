using com.unity.testtrack.Data;
using com.unity.testtrack.physics;
using UnityEngine;

namespace com.unity.testtrack.vehicle
{
    [RequireComponent(typeof(WheelCollider))]
    public class WheelColliderPhysicalMaterialResponse : MonoBehaviour
    {
        [SerializeField] WheelPhysicalProperty.Wheel    m_wheel;
        [SerializeField] CollidingPhysicalMaterial      m_collidingMaterial;

        private WheelCollider   m_wheelCollider;
        private Vector2         m_defaultStiffness = Vector2.zero;

        private void OnEnable()
        {
            m_wheelCollider = GetComponent<WheelCollider>();
            m_defaultStiffness = new Vector2(m_wheelCollider.forwardFriction.stiffness, m_wheelCollider.sidewaysFriction.stiffness);
            m_collidingMaterial?.Subscribe(OnPhysicalMaterialChange);
        }

        private void OnDisable()
        {
            m_collidingMaterial?.Unsubscribe(OnPhysicalMaterialChange);

        }

        /// <summary>
        /// Listen to Physical material change and set the right friction model
        /// </summary>
        /// <param name="physicProperty"></param>
        void OnPhysicalMaterialChange(WheelPhysicalProperty physicProperty)
        {
            var prop = GetWheelPhysicalPropertyValue(physicProperty);
            if (prop != null && prop.m_physicInfo != null)
            {
                WheelFrictionCurve wfc = m_wheelCollider.forwardFriction;
                wfc.stiffness = prop.m_physicInfo.m_forwardFrictionStiffness;
                m_wheelCollider.forwardFriction = wfc;

                wfc = m_wheelCollider.sidewaysFriction;
                wfc.stiffness = prop.m_physicInfo.m_sidewayFrictionStiffness;
                m_wheelCollider.sidewaysFriction = wfc;

            }
            else //Set default friction model
            {
                WheelFrictionCurve wfc = m_wheelCollider.forwardFriction;
                wfc.stiffness = m_defaultStiffness.x;
                m_wheelCollider.forwardFriction = wfc;

                wfc = m_wheelCollider.sidewaysFriction;
                wfc.stiffness = m_defaultStiffness.y;
                m_wheelCollider.sidewaysFriction = wfc;
            }
        }

        PhysicalProperty GetWheelPhysicalPropertyValue(WheelPhysicalProperty physicProperty)
        {
            switch (m_wheel)
            {
                case WheelPhysicalProperty.Wheel.FL: return physicProperty.fL;
                case WheelPhysicalProperty.Wheel.FR: return physicProperty.fR;
                case WheelPhysicalProperty.Wheel.RL: return physicProperty.rL;
                case WheelPhysicalProperty.Wheel.RR: return physicProperty.rR;
            };

            return null;
        }
    }
}
