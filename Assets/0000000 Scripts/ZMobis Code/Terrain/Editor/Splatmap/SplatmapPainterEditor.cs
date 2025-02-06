using UnityEditor;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	[CustomEditor(typeof(SplatmapPainter))]
	public class SplatmapPainterEditor : Editor
	{
		public void OnEnable()
		{
			var proxy = target as SplatmapPainter;
			if (proxy.m_material == null)
				proxy.m_material = new Material(Shader.Find("Unlit/DefaultMeshSplatting"));
			if (proxy.m_splatDefinition == null)
				proxy.m_splatDefinition = AssetDatabase.LoadAssetAtPath<AlphaSplatDefinition>("Assets/Content/Props/Manmade/HandlingTrack/Alphasplats/DefaultDefinition.asset");
		}
	}
}

