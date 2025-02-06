using UnityEditor;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	[CustomEditor(typeof(SplatmapProxy))]
	public class SplatmapProxyEditor : Editor
	{
		public void OnEnable()
		{
			var proxy = target as SplatmapProxy;
			if (proxy.m_material == null)
				proxy.m_material = new Material(Shader.Find("Unlit/DefaultMeshSplatting"));
		}
	}
}