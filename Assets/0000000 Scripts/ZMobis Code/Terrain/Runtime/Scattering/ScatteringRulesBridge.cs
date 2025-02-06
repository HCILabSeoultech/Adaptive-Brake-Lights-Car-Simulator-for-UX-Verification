using com.unity.testtrack.partioning.grid;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.unity.testtrack.terrainsystem
{
	/// <summary>
	/// Reprsentation a paint request in our system
	/// </summary>
	[System.Serializable]
	public class PaintRequest
	{
		public enum RuleRequestType
		{
			Tree = 0,
			Detail,
			Count,
		}

		public RuleRequestType type;
		public ScatteringRule component;
		public Vector3 uv;
		public float rotation;
		public Vector3 scale;
		public string name;

		public PaintRequest(ScatteringRule component, Vector3 uv, float rotation, Vector3 scale)
		{
			this.type = component.isDetails ? RuleRequestType.Detail : RuleRequestType.Tree;
			this.component = component;
			this.name = this.component.name;

			this.uv = uv;
			this.rotation = rotation;
			this.scale = scale;
		}

		public bool IsInBrushRadius(Vector2 uv, float sqrRadius)
		{
			Vector2 offset = new Vector2(this.uv.x - uv.x, this.uv.z - uv.y);
			if (Vector2.SqrMagnitude(offset) <= sqrRadius)
				return true;

			return false;
		}
	}

	/// <summary>
	/// Wrappe a tree instance into a CellData so our grid partionning system can understand it
	/// </summary>
	public class TreeInstanceWrapper : CellData
	{
		public TreeInstance instance;

		public Vector3 worldPosition => ScatteringRulesUtils.TerrainToWorld(instance.position, terrainSize) + terrainPosition;

		public Vector3 uv => instance.position;

		public Vector3 position
		{
			get { return worldPosition; }
			set
			{
				throw new NotImplementedException();
			}
		}
		public Bounds Bounds { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public ScatteringRulesBridge m_bridge;
		public Vector3 terrainPosition;
		public Vector3 terrainSize;
		public bool bIsWaitingCreation;

		public TreeInstanceWrapper()
		{

		}

		public void Set(ScatteringRulesBridge bridge, Terrain terrain, TreeInstance tree)
		{
			m_bridge = bridge;
			instance = tree;
			terrainPosition = terrain.GetPosition();
			terrainSize = terrain.terrainData.size;
			bIsWaitingCreation = false;
		}

		public void Set(ScatteringRulesBridge bridge, Vector3 terrainPosition, Vector3 terrainSize, TreeInstance tree)
		{
			m_bridge = bridge;
			instance = tree;
			this.terrainPosition = terrainPosition;
			this.terrainSize = terrainSize;
			bIsWaitingCreation = false;
		}
	}

	/// <summary>
	/// Wrappe a Paint Request into a CellData so our grid partionning system can understand it
	/// </summary>
	public class PaintRequestWrapper : CellData
	{
		public PaintRequest instance;

		public Vector3 worldPosition => ScatteringRulesUtils.TerrainToWorld(instance.uv, terrainSize) + terrainPosition;

		public Vector3 uv => instance.uv;

		public int arrayIndex => index;

		public Vector3 position
		{
			get { return worldPosition; }
			set
			{
				throw new NotImplementedException();
			}
		}
		public Bounds Bounds { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public ScatteringRulesBridge m_bridge;
		public int index;
		public Vector3 terrainPosition;
		public Vector3 terrainSize;

		public PaintRequestWrapper()
		{

		}

		public void Set(ScatteringRulesBridge bridge, Terrain terrain, PaintRequest request, int index)
		{
			m_bridge = bridge;
			instance = request;
			this.index = index;
			terrainPosition = terrain.GetPosition();
			terrainSize = terrain.terrainData.size;
		}

		public void Set(ScatteringRulesBridge bridge, Vector3 terrainPosition, Vector3 terrainSize, PaintRequest request, int index)
		{
			m_bridge = bridge;
			instance = request;
			this.index = index;
			this.terrainPosition = terrainPosition;
			this.terrainSize = terrainSize;
		}

		public void Update(int indexInArray)
		{
			index = indexInArray;
		}
	}

	[ExecuteAlways]
	public class ScatteringRulesBridge : MonoBehaviour
	{
		public bool				m_DisplayCells = false;
		private Terrain			m_targetTerrain;
		public ConcurrentDictionary<string, int>[] m_ruleToProtoIndex = new ConcurrentDictionary<string, int>[((int)ScatteringRule.RuleType.GameObject)+1] 
		{ 
			new ConcurrentDictionary<string, int>(), 
			new ConcurrentDictionary<string, int>(), 
			new ConcurrentDictionary<string, int>() 
		};

		public PartitionGrid				m_treePartitionning;
		public List<TreePrototype>			m_treePrototypes = new List<TreePrototype>();
		public List<DetailPrototype>		m_detailPrototypes = new List<DetailPrototype>();
		public List<TreeInstanceWrapper>	m_treeInstancesToCreate = new List<TreeInstanceWrapper>();
		public List<TreeInstanceWrapper>	m_treeInstancesToRemove = new List<TreeInstanceWrapper>();
		public List<IExclusionZone>			m_exclusionZones = new List<IExclusionZone>();


		private BrushRep								m_BrushRep;
		private TreePrototypeExtensionDataProvider		m_treePrototypeExtraDataProvider = null;
		private DetailPrototypeExtensionDataProvider	m_detailPrototypeExtraDataProvider = null;
		private SplatmapUpdateManager					m_splatmapManager = null;
		private ScatteringRules							m_rules;
		private bool									bIsInitialized = false;
		private static ObjectPool<TreeInstanceWrapper>	m_treeWrapperPool = new ObjectPool<TreeInstanceWrapper>();
		private ObjectPool<PaintRequestWrapper>			m_requestWrapperPool = new ObjectPool<PaintRequestWrapper>();

		public bool isValid { get { return m_targetTerrain != null; } }
		public void OnEnterToolMode(Terrain terrain)
		{
			m_targetTerrain = terrain;
			m_rules = m_targetTerrain.GetComponent<ScatteringRules>();
			m_splatmapManager = m_targetTerrain.GetComponent<SplatmapUpdateManager>();
			var treeInstances = m_targetTerrain.terrainData.treeInstances;
			m_treePrototypes = m_targetTerrain.terrainData.treePrototypes.ToList();
			m_detailPrototypes = m_targetTerrain.terrainData.detailPrototypes.ToList();

			UpdateRuleToProtoIndexes();

			m_treePrototypeExtraDataProvider = terrain.GetComponent<TreePrototypeExtensionDataProvider>();
			if (m_treePrototypeExtraDataProvider != null)
				FillTreePrototypeExtraData();

			m_detailPrototypeExtraDataProvider = terrain.GetComponent<DetailPrototypeExtensionDataProvider>();
			if (m_detailPrototypeExtraDataProvider != null)
				FillDetailPrototypeExtraData();

			var terrainPosition = m_targetTerrain.GetPosition();
			var terrainSize = m_targetTerrain.terrainData.size;
			var wrappers = treeInstances.Select((item, index) => 
														m_treeWrapperPool.Acquire(data => 
														data?.Set(this, terrainPosition, terrainSize, item))
														).ToArray();

			int partitionSize = Mathf.CeilToInt((m_targetTerrain.terrainData.size.x / 2500) * 100);
			partitionSize = partitionSize < 1 ? 1 : partitionSize;
			m_treePartitionning = new PartitionGrid(m_targetTerrain.GetPosition(), m_targetTerrain.terrainData.size, partitionSize, partitionSize, false);
			m_treePartitionning.Insert(wrappers);
#if UNITY_EDITOR
			EditorApplication.update += EditorUpdate;
			Undo.undoRedoPerformed += OnUndoRedo;
#endif
			bIsInitialized = true;

			//Gather exclustion zones
			var objs = GameObject.FindObjectsOfType<MonoBehaviour>(true).OfType<IExclusionZone>();
			if (objs != null)
			{
				m_exclusionZones = objs.ToList();
				for(int i = m_exclusionZones.Count-1; i >=0; i--)
				{
					if (!m_exclusionZones[i].enabled)
						m_exclusionZones.RemoveAt(i);
					else
						m_exclusionZones[i].ComputeBounds();
				}
			}
		}

		public void OnExitToolMode()
		{
			//Make sure we process our pending instances before leaving
			UpdateTerrainTreeInstances();

			bIsInitialized = false;
#if UNITY_EDITOR
			EditorApplication.update -= EditorUpdate;
			Undo.undoRedoPerformed -= OnUndoRedo;
#endif

			for(int i = 0; i < m_ruleToProtoIndex.Length; i++)
				m_ruleToProtoIndex[i].Clear();

			m_treePrototypes.Clear();
			m_detailPrototypes.Clear();
			m_exclusionZones.Clear();
			m_requestWrapperPool.Clear();

			if (m_treePartitionning != null)
			{
				var instances = m_treePartitionning.ToArray();
				foreach (var i in instances)
					m_treeWrapperPool.Realease(i as TreeInstanceWrapper);
				m_treePartitionning = null;
			}

			m_targetTerrain = null;
		}

		private void OnValidate()
		{
			for (int i = 0; i < m_ruleToProtoIndex.Length; i++)
				m_ruleToProtoIndex[i].Clear();
		}

		private float lastTimePainted = -1;
		public void EditorUpdate()
		{
			if (!bIsInitialized)
				return;

			if (Time.realtimeSinceStartup - lastTimePainted >= 0.25f && 
				(m_treeInstancesToCreate.Count > 0 || m_treeInstancesToRemove.Count > 0))
			{
				UpdateTerrainTreeInstances();
			}
		}

		private void OnUndoRedo()
		{
			if (m_targetTerrain.terrainData.treeInstanceCount != m_treePartitionning.GetDataCount())
			{
				var treeInstances = m_targetTerrain.terrainData.treeInstances;

				m_treePartitionning.Clear();
				m_treeInstancesToCreate.Clear();
				m_treeInstancesToRemove.Clear();

				var wrappers = Array.ConvertAll<TreeInstance, TreeInstanceWrapper>(treeInstances, item => m_treeWrapperPool.Acquire(data => data.Set(this, m_targetTerrain, item)));
				m_treePartitionning.Insert(wrappers);
			}
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = new Color(1.0f, 0.5f, 0.5f, 0.25f);
			foreach(var instance in m_treeInstancesToRemove)
			{
				var mesh = TerrainBrushPreviewMesh.GetMeshFromRequest(instance);
				Gizmos.DrawMesh(mesh, instance.worldPosition, Quaternion.Euler(0, instance.instance.rotation, 0), new Vector3(instance.instance.widthScale, instance.instance.heightScale, instance.instance.widthScale));
			}

			Gizmos.color = new Color(0.5f, 1.0f, 5.0f, 0.25f);
			foreach (var instance in m_treeInstancesToCreate)
			{
				var mesh = TerrainBrushPreviewMesh.GetMeshFromRequest(instance);
				Gizmos.DrawMesh(mesh, instance.worldPosition, Quaternion.Euler(0, instance.instance.rotation, 0), new Vector3(instance.instance.widthScale, instance.instance.heightScale, instance.instance.widthScale));
			}

			if (m_treePartitionning != null && m_DisplayCells)
				m_treePartitionning.OnDrawGizmosSelected();
		}

		public void UpdateTerrainTreeInstances()
		{
			TempTerrainPaintUtilityEditor.UpdateTerrainDataUndo(m_targetTerrain.terrainData, "Terrain - Paint Rules");

			var UpdateTreeArray = BlitNewTrees();
			UpdateTreeArray |= RemoveOldTrees();

			if (UpdateTreeArray)
			{
				var partitionInstances = m_treePartitionning.ToArray().ToArray();
				var instances = Array.ConvertAll<CellData, TreeInstance>(partitionInstances, item => (item as TreeInstanceWrapper).instance);
				m_targetTerrain.terrainData.SetTreeInstances(instances.ToArray(), true); //Doesn't preserve the ordering for some reason :(
			}
		}

		public bool BlitNewTrees()
		{
			bool bHaveAddedSomething = m_treeInstancesToCreate.Count > 0;
			for (int i = 0; i < m_treeInstancesToCreate.Count; i++)
			{
				bHaveAddedSomething = true;
				m_treeInstancesToCreate[i].bIsWaitingCreation = false;
			}
			m_treeInstancesToCreate.Clear();

			return bHaveAddedSomething;
		}

		public bool RemoveOldTrees()
		{
			bool bHaveRemovedSomething = m_treeInstancesToRemove.Count > 0;
			foreach (var wrapper in m_treeInstancesToRemove)
				m_treeWrapperPool.Realease(wrapper);
			m_treeInstancesToRemove.Clear();

			return bHaveRemovedSomething;
		}

		public void UpdateRuleToProtoIndexes()
		{
			var treeProtos = m_targetTerrain.terrainData.treePrototypes;
			var detailProtos = m_targetTerrain.terrainData.detailPrototypes;

			for (int i = 0; i < treeProtos.Length; i++)
			{
				if (!m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Tree].ContainsKey(treeProtos[i].prefab.name))
					m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Tree].TryAdd(treeProtos[i].prefab.name, i);
			}
			for (int i = 0; i < detailProtos.Length; i++)
			{
				if (!m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Details].ContainsKey(detailProtos[i].prototype.name))
					m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Details].TryAdd(detailProtos[i].prototype.name, i);
			}
		}

		public void FillTreePrototypeExtraData()
		{
			if (m_treePrototypeExtraDataProvider == null || m_rules == null)
				return;

			HashSet<ScatteringRule> allRules = new HashSet<ScatteringRule>();
			foreach (var rule in m_rules.rules)
			{
				List<ScatteringRule> tRules = new List<ScatteringRule>();
				ScatteringRulesUtils.GatherRules(rule.prefab, ref tRules, true);

				foreach (var r in tRules.Where(r => !allRules.Contains(r)))
					allRules.Add(r);
			}

			var treeProtos = m_targetTerrain.terrainData.treePrototypes;
			m_treePrototypeExtraDataProvider.Initialize(treeProtos, (TreePrototypeExtensionDataProvider.ExtraData ed) => {
				var protoName = ed.m_key.name;

				//Find our rule
				foreach (var r in allRules.Where(r => r.ruleType == ScatteringRule.RuleType.Tree))
				{
					if (r.name == protoName)
					{
						ed.alphamapSplatDefinition = r.alphamapSplatDefinition;
						ed.alphamapSplatMaterial = r.alphamapSplatMaterial;
						break;
					}
				}
			});
		}

		public void FillDetailPrototypeExtraData()
		{
			if (m_detailPrototypeExtraDataProvider == null)
				return;
			HashSet<ScatteringRule> allRules = new HashSet<ScatteringRule>();
			foreach (var rule in m_rules.rules)
			{
				List<ScatteringRule> tRules = new List<ScatteringRule>();
				ScatteringRulesUtils.GatherRules(rule.prefab, ref tRules, true);

				foreach (var r in tRules.Where(r => !allRules.Contains(r)))
					allRules.Add(r);
			}

			var protos = m_targetTerrain.terrainData.detailPrototypes;
			m_detailPrototypeExtraDataProvider.Initialize(protos, (DetailPrototypeExtensionDataProvider.ExtraData ed) => {
				var protoName = ed.m_key.name;

				//Find our rule
				foreach (var r in allRules.Where(r => r.ruleType == ScatteringRule.RuleType.Details))
				{
					if (r.name == protoName)
					{
						ed.alphamapSplatDefinition = r.alphamapSplatDefinition;
						ed.alphamapSplatMaterial = r.alphamapSplatMaterial;
						break;
					}
				}
			});
		}

#if UNITY_EDITOR
		public void Paint(UnityEditor.TerrainTools.IOnPaint editContext, PaintRequest[] requests)
		{
			lastTimePainted = Time.realtimeSinceStartup;
			for (int i = 0; i < m_ruleToProtoIndex.Length; i++)
				m_ruleToProtoIndex[i].Clear();
			UpdateRuleToProtoIndexes();

			//do
			var listRequests = requests.ToList();
			FilterByDistance(ref listRequests);
			if (listRequests.Count == 0)
				return;

			FilterByExclusionZones(ref listRequests);
			if (listRequests.Count == 0)
				return;

			int nbNewRequests = 0;
			List<PaintRequest> requestToApplyRulesTo = listRequests.ToList();
			do
			{
				nbNewRequests = ExecuteChildRules(requestToApplyRulesTo, out var newRequests);
				if (nbNewRequests > 0)
				{
					FilterByDistance(ref newRequests);
					FilterByExclusionZones(ref newRequests);
					listRequests.AddRange(newRequests);
				}
				requestToApplyRulesTo = newRequests;
			}
			while (nbNewRequests > 0);

			if (listRequests.Count > 0)
			{
				ExtractTreeRequests(listRequests, out var trees, out var details);

				if (trees.Count > 0)
				{
					//List<TreeInstance> instances = m_targetTerrain.terrainData.treeInstances.ToList();
					for (int i = 0; i < trees.Count; i++)
					{
						var request = trees[i];

						//Create instances
						var instance = new TreeInstance();
						instance.color = Color.white;
						instance.heightScale = request.scale.y;
						instance.widthScale = request.scale.x;
						instance.rotation = request.rotation * (Mathf.PI / 180.0f);
						instance.position = request.uv;
						instance.prototypeIndex = GetTreeProtoypeIndex(request.name);

						if (instance.prototypeIndex != -1)
						{
							var wrapper = m_treeWrapperPool.Acquire(data => data.Set(this, m_targetTerrain, instance));
							wrapper.bIsWaitingCreation = true;
							m_treeInstancesToCreate.Add(wrapper);
							m_treePartitionning.Insert(wrapper);
						}
					}
				}

				if (details.Count > 0)
				{
					TempTerrainPaintUtilityEditor.UpdateTerrainDataUndo(m_targetTerrain.terrainData, "Terrain - Paint Rules");
					WriteDetailsToTerrain(m_targetTerrain, editContext, details);
				}
			}

			lastTimePainted = Time.realtimeSinceStartup;
		}

		void WriteDetailsToTerrain(Terrain terrain, UnityEditor.TerrainTools.IOnPaint editContext, List<PaintRequest> details)
		{
			var terrainData = terrain.terrainData;
			float brushSize = 1;
			int size = (int)Mathf.Max(1.0f, brushSize * ((float)terrainData.detailResolution / terrainData.size.x));

			foreach (var dl in details)
			{
				ApplyDetails(m_targetTerrain, editContext, new Vector2(dl.uv.x, dl.uv.z), 1.0f, 1.0f, 16, new ScatteringRule[] { dl.component }.ToList());
			}
		}

		void ApplyDetails(Terrain terrain, UnityEditor.TerrainTools.IOnPaint editContext, Vector2 uv, float brushSize, float targetStrength, int maxStrength = 4, List<ScatteringRule> filters = null, bool forceOpacity = false, IExclusionZone zone = null)
		{
			if (terrain == null)
				return;

			TerrainData terrainData = terrain.terrainData;
			var detailOpacity = 1.0f;
			int size = (int)Mathf.Max(1.0f, brushSize * ((float)terrainData.detailResolution / terrainData.size.x));
			translateToDetailMapCoordonate(uv, size / 2.0f, terrainData.detailWidth, terrainData.detailHeight, out var regionMin, out var regionMax, out var regionSize, out var regionCenter, out var intRadius, out var intFraction);

			if (m_BrushRep == null)
				m_BrushRep = new BrushRep();

			if (editContext != null)
				m_BrushRep.CreateFromBrush(editContext.brushTexture as Texture2D, size);

			if (regionMin.x >= terrainData.detailWidth || regionMin.y >= terrainData.detailHeight || regionMax.x <= 0 || regionMax.y <= 0)
				return;

			int xmin = Mathf.Clamp(regionMin.x, 0, terrainData.detailWidth - 1);
			int ymin = Mathf.Clamp(regionMin.y, 0, terrainData.detailHeight - 1);

			int xmax = Mathf.Clamp(regionMax.x, 0, terrainData.detailWidth);
			int ymax = Mathf.Clamp(regionMax.y, 0, terrainData.detailHeight);

			int width = xmax - xmin;
			int height = ymax - ymin;

			int[] layers = GetDetailLayers(filters).ToArray();
			if (targetStrength < 0.0F && layers.Length == 0)
				layers = terrainData.GetSupportedLayers(xmin, ymin, width, height);

			for (int i = 0; i < layers.Length; i++)
			{
				int[,] alphamap = terrainData.GetDetailLayer(xmin, ymin, width, height, layers[i]);

				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						int xBrushOffset = (xmin + x) - (regionCenter.x - intRadius + intFraction);
						int yBrushOffset = (ymin + y) - (regionCenter.y - intRadius + intFraction);
						float opa = forceOpacity ? 1.0f : detailOpacity * m_BrushRep.GetStrengthInt(xBrushOffset, yBrushOffset);

						float targetValue = Mathf.Lerp(alphamap[y, x], targetStrength * maxStrength, opa);

						//Nothing to do here
						if ((targetValue < 0 && alphamap[y, x] == 0) ||
							targetValue > 16 && alphamap[y, x] == 16)
							continue;

						Vector3 woldPos = new Vector3((xmin + x) / (float)terrainData.detailWidth, 0, (ymin + y) / (float)terrainData.detailHeight);
						woldPos = TerrainToWorld(woldPos, terrainData.size);
						woldPos += terrain.GetPosition();
						if (zone != null && !zone.InZone(woldPos))
							continue;

						alphamap[y, x] = Mathf.Min(Mathf.RoundToInt(targetValue - .5f + UnityEngine.Random.value), maxStrength);
					}
				}

				terrainData.SetDetailLayer(xmin, ymin, layers[i], alphamap);
			}
		}

		public void Erase(UnityEditor.TerrainTools.IOnPaint editContext, Vector2 uv, float radius, List<ScatteringRule> filters = null)
		{
			lastTimePainted = Time.realtimeSinceStartup;
			TempTerrainPaintUtilityEditor.UpdateTerrainDataUndo(m_targetTerrain.terrainData, "Terrain - Erase Rules");

			UpdateRuleToProtoIndexes();
			var filterList = BuildFilterList(filters);
			var detailFilters = FilterRules(filters, true);

			float sqrRadius = radius * radius;
			var worldRadius = radius * m_targetTerrain.terrainData.size.x;
			var worldPos = TerrainToWorld(new Vector3(uv.x, 0, uv.y), m_targetTerrain.terrainData.size) + m_targetTerrain.GetPosition();
			if (filterList == null || filterList.Count == 0)
			{
				m_treePartitionning?.Remove(worldPos, worldRadius, (instanceWrapper) =>
				{
					var treeInstanceWrapper = instanceWrapper as TreeInstanceWrapper;
					m_treeInstancesToRemove.Add(treeInstanceWrapper);
				});
				ApplyDetails(m_targetTerrain, editContext, uv, (radius * 2) * m_targetTerrain.terrainData.size.x, -1, 4, detailFilters);
			}
			else 
			{
				m_treePartitionning?.Remove(worldPos, worldRadius, (instanceWrapper) =>
				{
					var treeInstanceWrapper = instanceWrapper as TreeInstanceWrapper;
					m_treeInstancesToRemove.Add(treeInstanceWrapper);
				},
				(instanceWrapper) =>
				{
					var treeInstanceWrapper = instanceWrapper as TreeInstanceWrapper;
					return filterList.Contains(treeInstanceWrapper.instance.prototypeIndex);
				});

				if (detailFilters.Count > 0)
					ApplyDetails(m_targetTerrain, editContext, uv, (radius * 2) * m_targetTerrain.terrainData.size.x, -1, 4, detailFilters);
			}
			lastTimePainted = Time.realtimeSinceStartup;
		}

		public void UpdateExclusionZones()
		{
			foreach (var zone in m_exclusionZones)
			{
				var cells = zone.GetIntersectingCells(m_treePartitionning);
				if (cells == null && cells.Count() == 0)
					continue;

				var detailFilters = FilterRules(zone.filters, true);
				foreach (var cell in cells.Where(cell => cell.hasData))
				{
					for (int i = cell.Count - 1; i>= 0; i--)
					{
						var wrapper = cell.data[i] as TreeInstanceWrapper;
						var proto = m_treePrototypes[wrapper.instance.prototypeIndex];
						if (zone.InZone(wrapper.position, proto.prefab.name))
						{
							m_treeInstancesToRemove.Add(wrapper);
							cell.RemoveData(cell.data[i]);
						}
					}
				}

				if ((zone.filters.Count != 0 && detailFilters.Count != 0) ||
						(zone.filters.Count == 0))
				{
					foreach (var cell in cells)
					{
						Vector3 uv = cell.bounds.center;
						float radius = cell.bounds.size.x;
						ApplyDetails(m_targetTerrain, null, new Vector2(uv.x, uv.z), (radius * 2) * m_targetTerrain.terrainData.size.x, -1, 4, detailFilters, true, zone);
					}
				}
			}
		}
#endif

		int GetDetailLayerIndex(string name)
		{
			if (m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Details].ContainsKey(name))
				return m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Details][name];

			return -1;
		}

		IEnumerable<int> GetDetailLayers(List<ScatteringRule> rules)
		{
			List<int> indexes = new List<int>();
			if (rules != null)
			{
				foreach (var r in rules)
				{
					var index = GetDetailLayerIndex(r.name);
					if (index != -1)
						indexes.Add(index);
				}
			}

			return indexes;
		}

		private List<ScatteringRule> FilterRules(List<ScatteringRule> rules, bool detail)
		{
			if (rules == null)
				return null;

			List<ScatteringRule> newRules = new List<ScatteringRule>(rules.Count);
			foreach(var f in rules.Where(f =>f.isDetails == detail))
				newRules.Add(f);

			return newRules;
		}

		private List<int> BuildFilterList(List<ScatteringRule> filters)
		{
			if (filters == null)
				return null;

			List<int> list = new List<int>(filters.Count);
			foreach (var filter in filters)
				list.Add(GetTreeProtoypeIndex(filter.name));

			return list;
		}

		private bool IsInBrush(TreeInstance instance, Vector2 uv, float sqrRadius)
		{
			Vector2 offset = new Vector2(instance.position.x - uv.x, instance.position.z - uv.y);
			if (Vector2.SqrMagnitude(offset) <= sqrRadius)
				return true;

			return false;
		}

		public bool isRequestIncludingInList(TreeInstance instance, List<ScatteringRule> rules)
		{
			foreach (var r in rules.Where(r => m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Tree].ContainsKey(r.name) && m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Tree][r.name] == instance.prototypeIndex))
				return true;

			return false;
		}

		public int ExecuteChildRules(List<PaintRequest> requests, out List<PaintRequest> newRequests)
		{
			newRequests = new List<PaintRequest>();

			for (int i = 0; i < requests.Count; i++)
			{
				var defs = requests[i].component.GetScatteringDefinitions(requests[i].scale);
				var requestScale = requests[i].scale;
				var defsCount = defs.Length;
				for (int j = 0; j < defsCount; j++)
				{
					var d = defs[j];
					var rule = d.prefab.GetComponent<ScatteringRule>();
					var scaleHeight = UnityEngine.Random.Range(rule.minMaxHeight.x, rule.minMaxHeight.y);
					var scaleWidth = rule.lockWidthToHeight ? scaleHeight : UnityEngine.Random.Range(rule.minMaxWidth.x, rule.minMaxWidth.y);
					var scale = new Vector3(scaleWidth * requestScale.x, scaleHeight * requestScale.y, scaleWidth * requestScale.z);
					var rotation = UnityEngine.Random.Range(0, 360);
					var position = requests[i].uv + WorldToTerrain(d.localPosition);

					var request = new PaintRequest(rule, position, rotation, scale);
					newRequests.Add(request);
				}
			}

			return newRequests.Count;
		}

		public void FilterByExclusionZones(ref List<PaintRequest> requests)
		{
			for (int i = requests.Count -1; i >= 0; i--)
			{
				var request = requests[i];
				var worldPos = TerrainToWorld(request.uv, m_targetTerrain.terrainData.size) + m_targetTerrain.GetPosition();
				foreach(var zone in m_exclusionZones)
				{
					if (zone.InZone(worldPos, request.name))
					{
						requests.RemoveAt(i);
						break;
					}
				}
			}
		}

		public void UpdateSplatmaps()
		{
			m_splatmapManager?.RegenerateAll();
		}

		public void ClearSplatmaps()
		{
			m_splatmapManager?.Clear();
		}

		public void FilterByDistance(ref List<PaintRequest> requests)
		{
			int partitionSize = Mathf.CeilToInt((m_targetTerrain.terrainData.size.x / 2500) * 100);
			partitionSize = partitionSize < 1 ? 1 : partitionSize;
			var grid = new PartitionGrid(m_targetTerrain.GetPosition(), m_targetTerrain.terrainData.size, partitionSize, partitionSize, false);

			var requestArray = requests.ToArray();
			var wrappers = requestArray.Select((item, index) => m_requestWrapperPool.Acquire(data => data.Set(this, m_targetTerrain, item, index))).ToArray();
			grid.Insert(wrappers);

			var itemsTooClose = GetObjectWithingMinDistance(requests, grid);
			RemoveInstances(ref requests, itemsTooClose);

			var data = grid.ToArray();
			foreach (var r in data)
				m_requestWrapperPool.Realease(r as PaintRequestWrapper);
		}

		private int[][,] GetAllDetailLayerData(int minx, int miny, int width, int height)
		{
			var layers = m_targetTerrain.terrainData.GetSupportedLayers(minx, miny, width, height);
			
			//find biggest layer index
			int biggestIndex = -1;
			foreach (var l in layers)
				biggestIndex = l > biggestIndex ? l : biggestIndex;

			int[][,] maps = new int[biggestIndex + 1][,];
			for (int i = 0; i < layers.Length; i++)
				maps[layers[i]] = m_targetTerrain.terrainData.GetDetailLayer(minx, miny, width, height, layers[i]);

			return maps;
		}

		List<int> GetObjectWithingMinDistance(List<PaintRequest> requests, PartitionGrid requestsGrid)
		{
			//Get any data we want from unity before going in parallel execution
			var terrainData = m_targetTerrain.terrainData;
			var terrainSize = terrainData.size;
			var terrainPosition = m_targetTerrain.GetPosition();
			var detailWidth = terrainData.detailWidth;
			var detailHeight = terrainData.detailHeight;
			var maps = GetAllDetailLayerData(0, 0, detailWidth, detailHeight);

			ConcurrentDictionary<int, int> instancesToRemove = new ConcurrentDictionary<int, int>();
			//Parallel.For(0, requests.Count, (i) =>
			for (int i = 0; i < requests.Count; i++)
			{
				var request = requests[i];
				//First see if we are on the terrain
				if (request.uv.x <= 0 || request.uv.z <= 0 || request.uv.x  >= 1.0f || request.uv.z >= 1.0f)
				{
					instancesToRemove.TryAdd(i, i);
					continue; // return;
				}

				if (request != null && request.component.minDistance > 0)
				{
					var worldPos = TerrainToWorld(request.uv, terrainSize) + terrainPosition;
					//Look within ourself
					if (requestsGrid.HasInstanceWithin(worldPos, request.component.minDistance, (cellData) =>
					{
						var wrapper = cellData as PaintRequestWrapper;
						var instance = wrapper.instance;

						return !instancesToRemove.ContainsKey(wrapper.index) &&
								i != wrapper.index &&
								request.name == instance.name;
					}))
					{
						instancesToRemove.TryAdd(i, i);
						continue; //return;
					}


					if (request.component.isDetails)
					{
						if (NeedToRemove(request, maps, detailWidth, detailHeight))
						{
							instancesToRemove.TryAdd(i, i);
							continue; //return;
						}
					}
					else
					{
						var protoIndex = GetTreeProtoypeIndex(request.name);
						if (m_treePartitionning.HasInstanceWithin(worldPos, request.component.minDistance, (cellData) =>
							{
								var wrapper = cellData as TreeInstanceWrapper;
								return wrapper.instance.prototypeIndex == protoIndex;
							}))						
						//if (NeedToRemove(request, terrainSize, terrainPosition, treeInstances))
						{
							instancesToRemove.TryAdd(i, i);
							continue; //return;
						}
					}
				}
			}//);

			var list = instancesToRemove.Values.ToList();
			list.Sort();
			return list;
		}

		private bool NeedToRemove(PaintRequest request, int[][,] maps, int detailWidth, int detailHeight)
		{
			bool bNeedToRemove = false;
			var uv = request.uv;
			var radius = request.component.minDistance / 2.0f;
			var detailLayer = GetDetailLayerIndex(request.name);
			if (detailLayer >= 0 && detailLayer < maps.Length && maps[detailLayer] != null)
			{
				//Look if there is something in the layer around the object
				translateToDetailMapCoordonate(new Vector2(uv.x, uv.z), radius, detailWidth, detailHeight, out var regionMin, out var regionMax, out var regionSize, out var regionCenter, out var intRadius, out var intFraction);

				if (regionMin.x < detailWidth && regionMin.y < detailHeight && regionMax.x > 0 && regionMax.y > 0)
				{
					int xmin = Mathf.Clamp(regionMin.x, 0, detailWidth - 1);
					int ymin = Mathf.Clamp(regionMin.y, 0, detailHeight - 1);

					int xmax = Mathf.Clamp(regionMax.x, 0, detailWidth);
					int ymax = Mathf.Clamp(regionMax.y, 0, detailHeight);

					int width = xmax - xmin;
					int height = ymax - ymin;

					//for (int y = 0; y < height; y++)
					Parallel.For(0, height, (y) =>
					{
						for (int x = 0; x < width; x++)
						{
							//if (alphamap[y, x] != 0)
							if (maps[detailLayer][y + ymin, x + xmin] != 0)
							{
								bNeedToRemove = true;
								return;
							}
						}
					});
				}
			}

			return bNeedToRemove;
		}

		private void translateToDetailMapCoordonate(Vector2 uv, float radius, int detailWidth, int detailHeight, out Vector2Int outMin, out Vector2Int outMax, out Vector2Int outSize, out Vector2Int outCenter, out int intRadius, out int intRadiusFrac)
		{
			outMin = Vector2Int.zero;
			outMax = Vector2Int.zero;
			outSize = Vector2Int.zero;
			outCenter = Vector2Int.zero;
			int size = (int)Mathf.Max(1.0f, radius * 2);

			Vector2 ctxUV = uv;

			outCenter.x = Mathf.FloorToInt(ctxUV.x * detailWidth);
			outCenter.y = Mathf.FloorToInt(ctxUV.y * detailHeight);

			intRadius = Mathf.RoundToInt(size) / 2;
			intRadiusFrac = Mathf.RoundToInt(size) % 2;

			outMin.x = outCenter.x - intRadius;
			outMin.y = outCenter.y - intRadius;

			outMax.x = outCenter.x + intRadius + intRadiusFrac;
			outMax.y = outCenter.y + intRadius + intRadiusFrac;

			outSize.x = outMax.x - outMin.x;
			outSize.y = outMax.y - outMin.y;
		}

		void RemoveInstances(ref List<PaintRequest> instances, List<int> itemsToRemove)
		{	
			for (int i = itemsToRemove.Count - 1; i >= 0; i--)
				instances.RemoveAt(itemsToRemove[i]);
		}

		public void ExtractTreeRequests(List<PaintRequest> requests, out List<PaintRequest> treeRequests, out List<PaintRequest> detailRequests)
		{
			treeRequests = new List<PaintRequest>();
			detailRequests = new List<PaintRequest>();

			var ordered = requests.OrderBy(x => x.component.isDetails);
			//find the separation point
			var lastIndexTree = Array.FindLastIndex<PaintRequest>(ordered.ToArray(), item => !item.component.isDetails) + 1;
			if (lastIndexTree <= 0)
				detailRequests = requests.ToList();
			else if (lastIndexTree == requests.Count-1)
				treeRequests = requests.ToList();
			else
			{
				var orderedList = ordered.ToList();
				detailRequests.AddRange(orderedList.GetRange(lastIndexTree, requests.Count - lastIndexTree));
				treeRequests.AddRange(orderedList.GetRange(0, lastIndexTree));
			}
		}

		Vector3 WorldToTerrain(Vector3 pos)
		{
			pos.Set(pos.x / m_targetTerrain.terrainData.size.x, pos.y / m_targetTerrain.terrainData.size.y, pos.z / m_targetTerrain.terrainData.size.z);
			return pos;
		}
		Vector3 TerrainToWorld(Vector3 pos)
		{
			pos.Set(pos.x * m_targetTerrain.terrainData.size.x, pos.y * m_targetTerrain.terrainData.size.y, pos.z * m_targetTerrain.terrainData.size.z);
			return pos;
		}

		Vector3 TerrainToWorld(Vector3 pos, Vector3 terrainSize)
		{
			pos.Set(pos.x * terrainSize.x, pos.y * terrainSize.y, pos.z * terrainSize.z);
			return pos;
		}

		public int GetTreeProtoypeIndex(string name)
		{
			if (m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Tree].ContainsKey(name))
				return m_ruleToProtoIndex[(int)ScatteringRule.RuleType.Tree][name];

			return -1;
		}

		//Is is called for each instance that change the terrain,
		//So we should accumulate and process once per frame if needed
		void OnTerrainChanged(TerrainChangedFlags flag)
		{

		}
	}
}