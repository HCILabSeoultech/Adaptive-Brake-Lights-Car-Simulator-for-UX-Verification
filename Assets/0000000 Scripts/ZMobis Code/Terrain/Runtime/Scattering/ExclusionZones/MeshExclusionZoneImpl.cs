using com.unity.testtrack.partioning.grid;
using com.unity.testtrack.utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using static RenderOnDemandUtils;

namespace com.unity.testtrack.terrainsystem
{
	public class ExclusionProxyRenderOnDemand : IRenderOnDemand
	{
		public ExclusionProxyRenderOnDemand(ExclusionProxy[] objects, Material material = null)
		{
			var lmaterial = material;

			m_meshes = new List<RenderMeshPass>();
			foreach (var obj in objects)
			{
				var lodGroup = obj.GetComponent<LODGroup>();
				var meshFilter = obj.GetComponent<MeshFilter>();
				var meshRenderer = obj.GetComponent<MeshRenderer>();
				if (lodGroup != null)
				{
					var lods = lodGroup.GetLODs();
					var lod = 0;//lods.Length - 1;
					lod = Mathf.Clamp(lod, 0, lods.Length - 1);

					var renderer = lods[lod].renderers[0];
					meshFilter = renderer.GetComponent<MeshFilter>();
				}

				if (meshFilter != null)
					m_meshes.Add(new RenderMeshPass(meshFilter.sharedMesh, material != null ? material : obj.m_material, meshFilter.transform));
			}
		}

		List<RenderMeshPass> m_meshes;
		public List<RenderMeshPass> meshes => m_meshes;
	}

	public class MeshExclusionZoneImpl : IExclusionZone
	{
		#region User inputs
		public Resolutions						m_resolution = Resolutions._128x128;
		public Material							m_material = null;
		public List<ScatteringRule>				m_filters = new List<ScatteringRule>();
		public bool								m_includeSubRules = true;
		[Header("Debug")]
		public bool								m_drawBounds = false;
		public bool								m_drawIntersectingCells = false;
		private HashSet<Cell>					m_intersectingCells = new HashSet<Cell>();
		public RawImage							m_previewOutputImage;
		#endregion

		public enum Resolutions
		{
			_32x32 = 32,
			_64x64 = 64,
			_128x128 = 128,
			_256x256 = 256,
			_512x512 = 512,
			_1024x1024 = 1024,
			_2048x2048 = 2048,
		}

		private Material		m_materialCopy;
		private Bounds			m_computedBounds;
		private HashSet<ScatteringRule>		m_computedFilters = new HashSet<ScatteringRule>();
		private Texture2D		m_capture;
		private Color[]			m_pixels;
		private ExclusionProxy[] m_proxies = null;

		override public Vector3 center { get { return m_computedBounds.center; } }
		override public Vector3 size { get { return m_computedBounds.size; } }
		override public float radius { get { return size.magnitude * 0.5f; } }

		override public List<ScatteringRule> filters { get { return m_computedFilters.ToList(); } }

		override public void ComputeBounds()
		{
			m_proxies = gameObject.GetComponentsInChildren<ExclusionProxy>(true);
			InternalComputeBounds();
			ComputeRules();

			RenderTextureDescriptor descriptor = new RenderTextureDescriptor
			{
				height = (int)m_resolution,
				width = (int)m_resolution,
				volumeDepth = 1,
				enableRandomWrite = true,
				graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, // we need 16 bit precision for the distance field
				useMipMap = false,
				msaaSamples = 1,
				mipCount = 10,
				autoGenerateMips = true,
				dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
			};
			var tempRenderTarget = RenderTexture.GetTemporary(descriptor);

			m_materialCopy = null;
			if (m_material != null)
				m_materialCopy = new Material(m_material);

			if (m_proxies == null || m_proxies.Length == 0)
			{
				RenderOnDemandUtils.Paint(new RenderOnDemandUtils.GameObjectRenderOnDemand(
												new GameObject[] { gameObject },
												m_materialCopy),
												tempRenderTarget,
												m_computedBounds);
			}
			else
			{
				RenderOnDemandUtils.Paint(new ExclusionProxyRenderOnDemand(
												m_proxies,
												m_materialCopy),
												tempRenderTarget,
												m_computedBounds);
			}

			m_capture = tempRenderTarget.ToTexture2D();
			m_pixels = m_capture.GetPixels();
			if (m_previewOutputImage != null)
				m_previewOutputImage.texture = m_capture;
			RenderTexture.ReleaseTemporary(tempRenderTarget);
		}

		private void InternalComputeBounds()
		{
			if(m_proxies == null || m_proxies.Length == 0)
				gameObject.ComputeBounds(out m_computedBounds, false);
			else
			{
				var boundsWasSet = false;
				m_computedBounds = new Bounds(gameObject.transform.position, Vector3.zero);
				foreach(var proxy in m_proxies)
				{
					if (proxy.gameObject.ComputeBounds(out var bounds, false))
					{
						if (boundsWasSet)
							m_computedBounds.Encapsulate(bounds);
						else 
						{
							m_computedBounds = bounds;
							boundsWasSet = true;
						}
					}
				}
			}
		}

		private void ComputeRules()
		{
			m_computedFilters.Clear();
			if (m_filters.Count == 0)
				return;

			if (m_includeSubRules)
			{
				List<ScatteringRule> foundRules = new List<ScatteringRule>();
				foreach (var filter in m_filters)
				{
					ScatteringRulesUtils.GatherRules(filter, ref foundRules, false);
					foreach (var r in foundRules)
					{
						if (!m_computedFilters.Contains(r))
							m_computedFilters.Add(r);
					}
				}

			}
			else
			{
				foreach (var filter in m_filters)
				{
					if (!m_computedFilters.Contains(filter))
						m_computedFilters.Add(filter);
				}
			}

		}

		override public IEnumerable<Cell> GetIntersectingCells(PartitionGrid grid)
		{
			HashSet<Cell> cells = new HashSet<Cell>();
			m_intersectingCells.Clear();

			if (m_capture != null)
			{
				var captureWidth = m_capture.width;
				var captureHeight = m_capture.height;
				var boundMin = m_computedBounds.min;
				var boundSize = m_computedBounds.size;

				//Parallel.For(0, captureWidth, (i) =>
				for (int i = 0; i < captureWidth; i++)
				{
					//Parallel.For(0, captureHeight, (j) =>
					for (int j = 0; j < captureHeight; j++)
					{
						var pixel = m_pixels[(j * captureHeight) + i];
						if (pixel != new Color(0, 0, 0, 0))
						{
							Vector3 worldPos = new Vector3(boundMin.x + ((i / (float)captureWidth) * boundSize.x),
								0,
								boundMin.z + ((j / (float)captureHeight) * boundSize.z));

							var newCells = grid.FindCells(worldPos, boundSize.magnitude / captureWidth);
							foreach (var cell in newCells.Where(cell => !cells.Contains(cell)))
							{
								lock (cells)
								{
									cells.Add(cell);
								}
							}
						}
					}
				}
			}

			m_intersectingCells = cells;
			return cells;
		}

		internal Vector3 WorldToGrid(Vector3 pos)
		{
			pos -= m_computedBounds.min;
			pos.Set(pos.x / size.x, pos.y / size.y, pos.z / size.z);
			return pos;
		}

		override public bool InZone(Vector3 position, string objectName = null)
		{
			//Process our filters
			bool shouldCheck = string.IsNullOrEmpty(objectName) || filters.Count == 0;
			if (!string.IsNullOrEmpty(objectName) && filters.Count > 0)
			{
				foreach (var r in filters.Where(r => r.name == objectName))
				{
					shouldCheck = true;
					break;
				}
			}

			if (shouldCheck)
			{
				var pos = position - m_computedBounds.min;

				var tx = (int)Mathf.Floor(pos.x / (m_computedBounds.size.x / m_capture.width));
				var ty = (int)Mathf.Floor(pos.z / (m_computedBounds.size.z / m_capture.height));
				if (tx >= 0 && tx < m_capture.width && ty >= 0 && ty < m_capture.height)
				{
					var pixel = m_pixels[(ty * m_capture.height) + tx];//m_capture.GetPixel(tx, ty);
					if (pixel != new Color(0, 0, 0, 0))
						return true;
				}
			}

			return false;
		}

		public void OnDrawGizmos()
		{
			if (m_drawBounds)
			{
				InternalComputeBounds();

				Gizmos.color = Color.white;
				Gizmos.DrawWireCube(center, size);
				Gizmos.color = Color.green;
				Gizmos.DrawWireSphere(center, radius);
			}

			if (m_drawIntersectingCells)
			{
				var tSize = Terrain.activeTerrain.terrainData.size;

				foreach (var cell in m_intersectingCells)
				{
					Gizmos.color = Color.magenta;
					Vector3 wpos = new Vector3(cell.bounds.center.x * tSize.x, cell.bounds.center.y * tSize.y, cell.bounds.center.z * tSize.z);
					wpos += Terrain.activeTerrain.GetPosition();
					wpos.y = m_computedBounds.center.y;

					Vector3 wsize = new Vector3(cell.bounds.size.x * tSize.x, cell.bounds.size.y * tSize.y, cell.bounds.size.z * tSize.z);
					wsize.y = Terrain.activeTerrain.terrainData.size.y;

					Gizmos.DrawWireCube(wpos, wsize);
				}
			}
		}
	}
}