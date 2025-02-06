using System.Collections.Generic;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	public class SplatmapPainterMaterialComparer : IComparer<SplatmapPainter>
	{
		public int Compare(SplatmapPainter x, SplatmapPainter y)
		{
			return x.GetMaterial().renderQueue - y.GetMaterial().renderQueue;
		}
	}

	public class SplatmapPainter : MonoBehaviour
	{
		public Material m_material;
		public AlphaSplatDefinition m_splatDefinition;

		public Mesh mesh
		{
			get
			{
				var lodGroup = GetComponent<LODGroup>();
				if (lodGroup != null)
				{
					var lods = lodGroup.GetLODs();
					var lod = 0;//lods.Length - 1;
					lod = Mathf.Clamp(lod, 0, lods.Length - 1);

					var renderer = lods[lod].renderers[0];
					return renderer.GetComponent<MeshFilter>().sharedMesh;
				}

				return GetComponent<MeshFilter>().sharedMesh;
			}
		}

		private Material m_defaultMaterial;
		public Material GetMaterial()
		{
			if (m_defaultMaterial == null)
				m_defaultMaterial = new Material(Shader.Find("Unlit/DefaultMeshSplatting"));
			return m_material == null ? m_defaultMaterial : m_material;
		}

		private void OnEnable()
		{
		}

		private void OnDisable()
		{

		}

		private void OnValidate()
		{
		}
	}
}