using UnityEngine;

namespace com.unity.testtrack.physics
{
	[CreateAssetMenu(fileName = "PhysicalInfo", menuName = "ScriptableObjects/Materials/Create Physical Info", order = 1)]
	public class PhysicalInfo : ScriptableObject
	{
		public float m_forwardFrictionStiffness = 1.0f;
		public float m_sidewayFrictionStiffness = 1.0f;
	}
}
