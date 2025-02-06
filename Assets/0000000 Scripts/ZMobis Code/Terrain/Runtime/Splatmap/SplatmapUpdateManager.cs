using com.unity.testtrack.utils;
using com.unity.testtrack.terrainsystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace com.unity.testtrack.terrainsystem
{
	/// <summary>
	/// Compare two tree instances
	/// </summary>
	public class TreeInstanceSplattingMaterialComparer : IComparer<TreeInstance>
	{
		private TreePrototypeExtensionDataProvider m_treePrototypeExtraDataProvider = null;
		private Terrain m_terrain = null;
		private TreePrototype[] m_prototypes;

		public TreeInstanceSplattingMaterialComparer(Terrain terrain, TreePrototypeExtensionDataProvider treePrototypeExtraDataProvider)
		{
			m_treePrototypeExtraDataProvider = treePrototypeExtraDataProvider;
			m_terrain = terrain;
			m_prototypes = m_terrain.terrainData.treePrototypes;
		}

		public int Compare(TreeInstance x, TreeInstance y)
		{
			var edx = m_treePrototypeExtraDataProvider.Get(m_prototypes[x.prototypeIndex]);
			var edy = m_treePrototypeExtraDataProvider.Get(m_prototypes[y.prototypeIndex]);

			return edx.alphamapSplatMaterial.renderQueue - edy.alphamapSplatMaterial.renderQueue;
		}
	}

	/// <summary>
	/// Responsable to write object in the splatmap when dirty
	/// </summary>
	public class SplatmapUpdateManager : MonoBehaviour
	{
		[Serializable]
		public class TerainAlphaMapData
		{
			public Terrain terrain;
			public List<Texture2D> maps = new List<Texture2D>();
		}

		//Constants
		private static int kMaxSplattingTextures = 4;

		public List<TerainAlphaMapData> m_basedAlphaMaps = new List<TerainAlphaMapData>();

		[Header("Debug")]
		public RawImage m_baseDebugImage;
		public RawImage m_transparentDebugImage;
		public RawImage m_outputDebugImage;

		private TreePrototypeExtensionDataProvider m_treePrototypeExtraDataProvider = null;
		private DetailPrototypeExtensionDataProvider m_detailPrototypeExtraDataProvider = null;
		private Dictionary<GameObject, GameObject> m_meshProtoCache = new Dictionary<GameObject, GameObject>();
		GameObject GetInstance(TreePrototype proto)
		{
			if (!m_meshProtoCache.ContainsKey(proto.prefab))
			{
				var instance = GameObject.Instantiate(proto.prefab);
				instance.SetActive(false);
				instance.hideFlags = HideFlags.HideAndDontSave;
				m_meshProtoCache.Add(proto.prefab, instance);
			}

			return m_meshProtoCache[proto.prefab];
		}

		void ClearCache()
		{
			foreach (var p in m_meshProtoCache)
				GameObject.DestroyImmediate(p.Value);

			m_meshProtoCache.Clear();
		}

		private static Material m_splatmapMergeMaterial;
		public Material GetSplatmapMergeMaterial()
		{
			if (m_splatmapMergeMaterial == null)
				m_splatmapMergeMaterial = new Material(Shader.Find("Unlit/MergeWithSplatmap"));
			return m_splatmapMergeMaterial;
		}

		[ContextMenu("Clear")]
		public void Clear()
		{
			PaintBaseAlphaMaps();
		}

		[ContextMenu("RegenerateAll")]
		public void RegenerateAll()
		{
			ClearCache();
			if (m_treePrototypeExtraDataProvider == null)
				m_treePrototypeExtraDataProvider = GetComponent<TreePrototypeExtensionDataProvider>();

			if (m_detailPrototypeExtraDataProvider == null)
				m_detailPrototypeExtraDataProvider = GetComponent<DetailPrototypeExtensionDataProvider>();

			PaintBaseAlphaMaps();
			PaintTerrainTrees();

			//Gather Paintable Objects
			var objects = GameObject.FindObjectsOfType<SplatmapPainter>(true);
			if (objects != null || objects.Length > 0)
				PaintGameObjects(objects);

			PaintDetails();
		}

		//Paint default alpha maps
		void PaintBaseAlphaMaps()
		{
			foreach (var amData in m_basedAlphaMaps.Where(amData => amData != null && amData.terrain != null))
			{
				var t = amData.terrain;
				var tamd = GetTerrainAlphaMapData(t);
				for (int i = 0; i < t.terrainData.alphamapTextureCount; i++)
				{
					var desc = RenderOnDemandUtils.GetDescriptor(t.terrainData.alphamapTextures[i]);
					var rt = RenderOnDemandUtils.GetTemporary(desc);

					var tAlphaMap = t.terrainData.alphamapTextures[i];
					if (tamd != null && tamd.maps != null && i < tamd.maps.Count && tamd.maps[i] != null)
						Graphics.Blit(tamd.maps[i], rt);
					else
						RenderOnDemandUtils.Clear(rt, i == 0 ? new Color(1, 0, 0, 0) : Color.clear);

					WriteToAlphaMap(i, t.terrainData.alphamapTextures[i], rt);
					RenderOnDemandUtils.ReleaseTemporary(rt);
				}
			}
		}

		TerainAlphaMapData GetTerrainAlphaMapData(Terrain terrain)
		{
			foreach (var data in m_basedAlphaMaps.Where(data => data.terrain == terrain))
				return data;

			return null;
		}

		List<Color[]> GetTreePrototypesMapControbutions(Terrain terrain)
		{
			var protos = terrain.terrainData.treePrototypes;
			List<Color[]> treeProtoContributions = new List<Color[]>(protos.Length);
			foreach (var p in protos)
			{
				AlphaSplatDefinition def = null;
				var extraData = m_treePrototypeExtraDataProvider?.Get(p);
				if (extraData != null)
					def = extraData.alphamapSplatDefinition;

				var contributions = AlphaSplatDefinition.GetAlphaMapsContributions(def, terrain.terrainData);
				treeProtoContributions.Add(contributions);
			}

			return treeProtoContributions;
		}

		bool ContributeToSplatMap(TreePrototype[] treePrototypes, TreeInstance instance)
		{
			var extraData = m_treePrototypeExtraDataProvider?.Get(treePrototypes[instance.prototypeIndex]);
			if (extraData == null || extraData.alphamapSplatMaterial == null)
				return false;

			return true;
		}

		List<Color[]> GetDetailPrototypesMapControbutions(Terrain terrain)
		{
			var protos = terrain.terrainData.detailPrototypes;
			List<Color[]> detailProtoContributions = new List<Color[]>(protos.Length);
			foreach (var p in protos)
			{
				AlphaSplatDefinition def = null;
				var extraData = m_detailPrototypeExtraDataProvider?.Get(p);
				if (extraData != null)
					def = extraData.alphamapSplatDefinition;

				var contributions = AlphaSplatDefinition.GetAlphaMapsContributions(def, terrain.terrainData);
				detailProtoContributions.Add(contributions);
			}

			return detailProtoContributions;
		}

		TreeInstance[] GetTreeInstanceToPaint(Terrain t)
		{
			List<TreeInstance> instances = new List<TreeInstance>();
			var treeInstances = t.terrainData.treeInstances;
			var treePrototypes = t.terrainData.treePrototypes;
			for (int instanceIdx = 0; instanceIdx < t.terrainData.treeInstanceCount; instanceIdx++)
			{
				var treeInstance = treeInstances[instanceIdx];
				if (ContributeToSplatMap(treePrototypes, treeInstance))
					instances.Add(treeInstance);
			}

			return instances.ToArray();
		}

		//Paint the terrain tree in the alpha maps
		void PaintTerrainTrees()
		{
			foreach (var amData in m_basedAlphaMaps.Where(amData => amData != null && amData.terrain != null))
			{
				var t = amData.terrain;
				var nbAlphaMaps = t.terrainData.alphamapTextures.Length;
				if (t.terrainData.alphamapTextures == null || t.terrainData.alphamapTextures.Length == 0)
					continue;

				if (nbAlphaMaps > kMaxSplattingTextures)
				{
					Debug.LogError("SplatmapUpdateManager only suppor up to " + kMaxSplattingTextures + " alphamps, Requesting " + nbAlphaMaps);
					continue;
				}

				TreeInstance[] instanceToPaint = GetTreeInstanceToPaint(t);
				if (instanceToPaint.Length == 0)
					continue;

				t.gameObject.ComputeBounds(out var bounds, true);

				Array.Sort(instanceToPaint, new TreeInstanceSplattingMaterialComparer(t, m_treePrototypeExtraDataProvider));
				List<Color[]> treeProtoContributions = GetTreePrototypesMapControbutions(t);

				//Create a render target for each layers
				var desc = RenderOnDemandUtils.GetDescriptor(t.terrainData.alphamapTextures[0]);
				var splatttedRTArray = CreateLayers(t.terrainData, desc);

				//Rander objects
				for (int i = 0; i < nbAlphaMaps; i++)
				{
					var treePrototypes = t.terrainData.treePrototypes;
					for (int instanceIdx = 0; instanceIdx < instanceToPaint.Length; instanceIdx++)
					{
						var treeInstance = instanceToPaint[instanceIdx];
						var contributions = treeProtoContributions[treeInstance.prototypeIndex][i];
						var treeProto = treePrototypes[treeInstance.prototypeIndex];
						var extraData = m_treePrototypeExtraDataProvider?.Get(treeProto);

						//TODO: We need to blend
						var mat = extraData.alphamapSplatMaterial;
						mat.SetColor("_Color", contributions);
						//if (contributions == new Color(0, 0, 0, 0))
						//	continue;

						var instance = GetInstance(treeProto);
						if (instance != null)
						{
							//in terrain relative space
							var pos = new Vector3(
								treeInstance.position.x * t.terrainData.size.x,
								treeInstance.position.y * t.terrainData.size.y,
								treeInstance.position.z * t.terrainData.size.z
								);
							pos = t.transform.TransformPoint(pos);

							var rot = Quaternion.Euler(0, treeInstance.rotation * (180.0f / Mathf.PI), 0); //rotation is in radian
							var scale = new Vector3(treeInstance.widthScale, treeInstance.heightScale, treeInstance.widthScale);
							instance.transform.SetPositionAndRotation(pos, rot);
							instance.transform.localScale = scale;

							PaintGameObject(instance, mat, splatttedRTArray[i], bounds, false);
						}
					}
				}

				//Merge the layers
				SplatLayers(splatttedRTArray, t.terrainData, desc);
				ReleaseLayers(splatttedRTArray);
			}
		}

		void PaintDetails()
		{
			foreach (var amData in m_basedAlphaMaps.Where(amData => amData != null && amData.terrain != null))
			{
				var t = amData.terrain;
				var terrainData = t.terrainData;
				if (t.terrainData.alphamapTextures == null || t.terrainData.alphamapTextures.Length == 0)
					continue;

				var nbAlphaMaps = t.terrainData.alphamapTextures.Length;
				if (nbAlphaMaps > kMaxSplattingTextures)
				{
					Debug.LogError("SplatmapUpdateManager only suppor up to " + kMaxSplattingTextures + " alphamps, Requesting " + nbAlphaMaps);
					continue;
				}

				var desc = RenderOnDemandUtils.GetDescriptor(t.terrainData.alphamapTextures[0]);
				var splatttedRTArray = CreateLayers(t.terrainData, desc);

				var contributions = GetDetailPrototypesMapControbutions(t);

				for (int alphamapIndex = 0; alphamapIndex < nbAlphaMaps; alphamapIndex++)
				{
					Texture2D texture = new Texture2D(terrainData.detailWidth, terrainData.detailHeight);
					for (int i = 0; i < texture.width; i++)
						for (int j = 0; j < texture.height; j++)
							texture.SetPixel(i, j, new Color(0, 0, 0, 0));

					for (int i = 0; i < terrainData.detailPrototypes.Length; i++)
					{
						var detailMap = terrainData.GetDetailLayer(0, 0, terrainData.detailResolution, terrainData.detailResolution, i);

						for (int j = 0; j < detailMap.GetLength(0); j++)
						{
							for (int k = 0; k < detailMap.GetLength(1); k++)
							{
								if (detailMap[k, j] > 0) //Seems I need to rotate the texture...
								{
									//paintSomething
									texture.SetPixel(j, k, contributions[i][alphamapIndex]);
								}
							}
						}
					}

					texture.Apply();
					Graphics.Blit(texture, splatttedRTArray[alphamapIndex]);
				}

				SplatLayers(splatttedRTArray, t.terrainData, desc);
				ReleaseLayers(splatttedRTArray);
			}
		}

		void PaintGameObjects(SplatmapPainter[] objects)
		{
			//Sort Game Objects by materials
			Array.Sort(objects, new SplatmapPainterMaterialComparer());

			foreach (var amData in m_basedAlphaMaps.Where(amData => amData != null && amData.terrain != null))
			{
				var t = amData.terrain;
				var nbAlphaMaps = t.terrainData.alphamapTextures.Length;
				if (t.terrainData.alphamapTextures == null || t.terrainData.alphamapTextures.Length == 0)
					continue;

				if (nbAlphaMaps > kMaxSplattingTextures)
				{
					Debug.LogError("SplatmapUpdateManager only suppor up to " + kMaxSplattingTextures + " alphamps, Requesting " + nbAlphaMaps);
					continue;
				}

				t.gameObject.ComputeBounds(out var bounds, true);

				//Calculate the contribution on each layer for the objects
				Dictionary<SplatmapPainter, Color[]> objectContributions = new Dictionary<SplatmapPainter, Color[]>();
				foreach (var obj in objects)
				{
					var contributions = AlphaSplatDefinition.GetAlphaMapsContributions(obj.m_splatDefinition, t.terrainData);
					objectContributions.Add(obj, contributions);
				}


				//Create a render target for each layers
				var desc = RenderOnDemandUtils.GetDescriptor(t.terrainData.alphamapTextures[0]);
				var splatttedRTArray = CreateLayers(t.terrainData, desc);

				//Rander objects
				for (int i = 0; i < nbAlphaMaps; i++)
				{
					foreach (var obj in objects.Where(obj => obj.enabled))
					{
						var contributions = objectContributions[obj];
						float totalContrib = 0;
						foreach (var c in contributions)
							totalContrib += c.r + c.g + c.b + c.a;

						////TODO: We need to blend instead of skipping
						//if (contributions[i] == new Color(0, 0, 0, 0))
						//	continue;

						var mat = obj.GetMaterial();
						mat.SetColor("_Color", contributions[i]);
						PaintGameObject(obj.gameObject, mat, splatttedRTArray[i], bounds, false);
					}
				}

				SplatLayers(splatttedRTArray, t.terrainData, desc);
				ReleaseLayers(splatttedRTArray);
			}
		}

		RenderTexture[] CreateLayers(TerrainData terrainData, RenderTextureDescriptor desc)
		{
			if (terrainData.alphamapTextureCount == 0)
				return null;

			//Create a render target for each layers
			var splatttedRTArray = new RenderTexture[kMaxSplattingTextures];
			for (int i = 0; i < kMaxSplattingTextures; i++)
			{
				splatttedRTArray[i] = RenderOnDemandUtils.GetTemporary(desc);
				RenderOnDemandUtils.Clear(splatttedRTArray[i], new Color(0, 0, 0, 0));
			}

			return splatttedRTArray;
		}

		void ReleaseLayers(RenderTexture[] layers)
		{
			//Release
			for (int i = 0; i < kMaxSplattingTextures; i++)
				RenderOnDemandUtils.ReleaseTemporary(layers[i]);
		}

		void SplatLayers(RenderTexture[] layers, TerrainData terrainData, RenderTextureDescriptor desc)
		{
			if (layers == null)
				return;

			//Merge the layers
			var nbAlphaMaps = terrainData.alphamapTextureCount;
			for (int i = 0; i < nbAlphaMaps; i++)
			{
				var outputRT = RenderOnDemandUtils.GetTemporary(desc);
				RenderOnDemandUtils.Clear(outputRT, new Color(0, 0, 0, 0));

				var baseRT = RenderOnDemandUtils.GetTemporary(desc);
				ReadBasedAlphaMap(i, terrainData, baseRT);

				var mergedMat = GetSplatmapMergeMaterial();
				mergedMat.SetFloat("_CombinedLayerContribution", 1);
				mergedMat.SetTexture("_Texture1", layers[i]);
				mergedMat.SetTexture("_Texture2", layers[(i + 1) % kMaxSplattingTextures]);
				mergedMat.SetTexture("_Texture3", layers[(i + 2) % kMaxSplattingTextures]);
				mergedMat.SetTexture("_Texture4", layers[(i + 3) % kMaxSplattingTextures]);
				Graphics.Blit(baseRT, outputRT, mergedMat);

				if (m_baseDebugImage != null)
					m_baseDebugImage.texture = baseRT.ToTexture2D();
				if (m_transparentDebugImage != null)
					m_transparentDebugImage.texture = layers[i % nbAlphaMaps].ToTexture2D();
				if (m_outputDebugImage != null)
					m_outputDebugImage.texture = outputRT.ToTexture2D();

				RegisterUndoAlphaMap(i, terrainData);
				WriteToAlphaMap(i, terrainData.alphamapTextures[i], outputRT);

				RenderOnDemandUtils.ReleaseTemporary(baseRT);
				RenderOnDemandUtils.ReleaseTemporary(outputRT);
			}
		}

		void PaintGameObject(GameObject go, Material mat, RenderTexture rt, Bounds bounds, bool clearRT)
		{
			var proxies = go.GetComponentsInChildren<SplatmapProxy>(true);
			if (proxies == null || proxies.Length == 0)
			{
				RenderOnDemandUtils.Paint(new RenderOnDemandUtils.GameObjectRenderOnDemand(
												new GameObject[] { go },
												mat),
												rt,
												bounds,
												clearRT);
			}
			else
			{
				var newArray = Array.ConvertAll(proxies, item => item.gameObject);
				RenderOnDemandUtils.Paint(new RenderOnDemandUtils.GameObjectRenderOnDemand(
												newArray,
												mat),
												rt,
												bounds,
												clearRT);
			}
		}


		#region AlphaMaps
		static protected void ReadBasedAlphaMap(int alphaMapIndex, TerrainData terrainData, RenderTexture output)
		{
			Graphics.Blit(terrainData.alphamapTextures[alphaMapIndex], output);
		}

		static protected void WriteToAlphaMap(int alphaMapIndex, Texture2D alphaMap, RenderTexture input)
		{
			var active = RenderTexture.active;
			RenderTexture.active = input;
			alphaMap.ReadPixels(new Rect(0, 0, input.width, input.height), 0, 0, true);
			alphaMap.Apply();
			RenderTexture.active = active;
		}

		static protected void RegisterUndoAlphaMap(int alphaMapIndex, TerrainData terrainData)
		{
			//var undoObjects = new List<UnityEngine.Object>();
			//undoObjects.Add(terrainData);
			//undoObjects.AddRange(terrainData.alphamapTextures);
			//Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Write to splatmaps");
		}
		#endregion
	}
}
