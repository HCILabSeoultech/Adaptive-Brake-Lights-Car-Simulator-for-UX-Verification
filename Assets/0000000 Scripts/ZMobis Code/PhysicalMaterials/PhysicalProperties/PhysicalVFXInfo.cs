using UnityEngine;
using UnityEngine.VFX;

namespace com.unity.testtrack.physics
{
	[CreateAssetMenu(fileName = "PhysicalVFXInfo", menuName = "ScriptableObjects/Materials/Create PhysicalVFXInfo", order = 1)]
	public class PhysicalVFXInfo : ScriptableObject
	{
		public enum Type
		{
			None,
			TireSmoke,
			TireRoll,
			TireSqueal,
		};
		public Type m_type;
		public AudioClip m_sound;
		public VisualEffectAsset m_effect;
		//public DecalScriptableObject decal;
	}
}
