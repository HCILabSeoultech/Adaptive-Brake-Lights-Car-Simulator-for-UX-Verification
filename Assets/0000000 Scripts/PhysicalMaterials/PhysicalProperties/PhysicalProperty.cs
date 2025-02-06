using System.Collections.Generic;
using UnityEngine;

namespace com.unity.testtrack.physics
{
	[CreateAssetMenu(fileName = "PhysicalProperty", menuName = "ScriptableObjects/Materials/Create PhysicalProperty", order = 1)]
    public class PhysicalProperty : ScriptableObject
    {
        public List<PhysicalVFXInfo> m_VFXInfos = new List<PhysicalVFXInfo>();
        public PhysicalInfo m_physicInfo;

        public bool Contains(PhysicalVFXInfo.Type type)
        {
            return m_VFXInfos.Exists(x => x.m_type == type);
        }
    }
}
