using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	[CustomEditor(typeof(ExclusionProxy))]
	public class ExclusionProxyEditor : Editor
	{
		public void OnEnable()
		{
			var proxy = target as ExclusionProxy;
			if (proxy.m_material == null)
				proxy.m_material = new Material(Shader.Find("Shader Graphs/DefaultExclusionZone"));
		}
	}
}
