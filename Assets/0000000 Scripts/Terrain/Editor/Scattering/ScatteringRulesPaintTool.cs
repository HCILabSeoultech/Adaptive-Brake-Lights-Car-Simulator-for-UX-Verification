using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TerrainTools;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEditorInternal;

namespace com.unity.testtrack.terrainsystem
{
	public class ScatteringRulesPaintTool : TerrainPaintTool<ScatteringRulesPaintTool>
	{
		public class TerrainScatteringToolExtension
		{
			private Terrain m_terrain;
			private ScatteringRulesBridge m_rulesBridge;

			public TerrainScatteringToolExtension(Terrain terrain)
			{
				m_terrain = terrain;
				m_rulesBridge = terrain.GetComponentInChildren<ScatteringRulesBridge>();
			}

			public void OnEnterToolMode()
			{
				if (m_rulesBridge != null && !m_rulesBridge.isValid)
					m_rulesBridge.OnEnterToolMode(m_terrain);
			}
			public void OnExitToolMode()
			{
				if (m_rulesBridge != null && m_rulesBridge.isValid)
					m_rulesBridge.OnExitToolMode();
			}

			public void Erase(UnityEditor.TerrainTools.IOnPaint editContext, Vector2 uv, float radius, List<ScatteringRule> filters = null)
			{
				if (m_rulesBridge == null)
					return;

				//Make sure the terrain tool is initialized
				OnEnterToolMode();
				m_rulesBridge.Erase(editContext, uv, radius, filters);
			}

			public void Paint(UnityEditor.TerrainTools.IOnPaint editContext, PaintRequest[] requests)
			{
				if (m_rulesBridge == null)
					return;

				//Make sure the terrain tool is initialized
				OnEnterToolMode();
				m_rulesBridge.Paint(editContext, requests);
			}

			public void UpdateExclusionZones()
			{
				if (m_rulesBridge == null)
					return;

				OnEnterToolMode();
				m_rulesBridge.UpdateExclusionZones();
			}

			public void UpdateSplatmaps()
			{
				if (m_rulesBridge == null)
					return;

				OnEnterToolMode();
				m_rulesBridge.UpdateSplatmaps();
			}

			public void ClearSplatmaps()
			{
				if (m_rulesBridge == null)
					return;

				OnEnterToolMode();
				m_rulesBridge.ClearSplatmaps();
			}

			public bool isValid { get { return m_rulesBridge != null && m_rulesBridge.isValid; } }
			public Bounds terrainBounds { get { return new Bounds(m_terrain.transform.localToWorldMatrix.MultiplyPoint3x4(m_terrain.terrainData.bounds.center), m_terrain.terrainData.bounds.size); ; } }
			public int exclusionZoneCount { get { return m_rulesBridge != null && m_rulesBridge.isValid ? m_rulesBridge.m_exclusionZones.Count : 0; } }
		}

		private Terrain m_TargetTerrain;
		private ScatteringRules m_rules;
		private SerializedObject m_rulesObject;

		private List<ScatteringRule> m_selectedRules = new List<ScatteringRule>();
		private Dictionary<Terrain, TerrainScatteringToolExtension> m_terrainExtensions = new Dictionary<Terrain, TerrainScatteringToolExtension>();

		public float brushSize { get; set; } = 40;
		public float spacing { get; set; } = 1.0f;

		private static Material m_BrushPreviewMaterial;
		public static Material GetDefaultBrushPreviewMaterial(Color color)
		{
			if (m_BrushPreviewMaterial == null)
				m_BrushPreviewMaterial = new Material(Shader.Find("Hidden/TerrainEngine/CustomBrushPreview"));

			m_BrushPreviewMaterial.SetColor("_Color", color);

			return m_BrushPreviewMaterial;
		}

		bool needUpdate = false;
		public override void OnEnterToolMode()
		{
			Terrain terrain = null;
			if (Selection.activeGameObject != null)
				terrain = Selection.activeGameObject.GetComponent<Terrain>();

			m_TargetTerrain = terrain;
			if (m_TargetTerrain == null)
				return;

			m_rules = terrain.GetComponent<ScatteringRules>();
			if (m_rules == null)
				m_rules = terrain.gameObject.AddComponent<ScatteringRules>();
			m_rulesObject = new SerializedObject(m_rules);

			foreach (var t in Terrain.activeTerrains)
				if (!m_terrainExtensions.ContainsKey(t))
					m_terrainExtensions.Add(t, new TerrainScatteringToolExtension(t));

			InitializeTerrain(m_TargetTerrain);
		}


		public override void OnExitToolMode()
		{
			foreach (var ext in m_terrainExtensions.Where(ext => ext.Value.isValid))
				ext.Value.OnExitToolMode();

			rlist = null;
			m_rules = null;
			m_rulesObject = null;

			m_terrainExtensions.Clear();
		}


		public override string GetName()
		{
			return UIConstants.toolName.text;
		}

		public override string GetDescription()
		{
			return UIConstants.toolDescription.text;
		}

		void InitializeTerrain(Terrain terrain)
		{
			if (m_terrainExtensions.ContainsKey(terrain) && !m_terrainExtensions[terrain].isValid)
				m_terrainExtensions[terrain].OnEnterToolMode();
		}

		public override bool OnPaint(Terrain terrain, IOnPaint editContext)
		{
			InitializeTerrain(terrain);
			if (!Event.current.shift && !Event.current.control)
			{
				if (isSelectionValid())
					PlaceRules(terrain, editContext);
			}
			else
			{
				RemoveRules(terrain, editContext, Event.current.shift);
			}

			return false;
		}

		public override void OnRenderBrushPreview(Terrain terrain, IOnSceneGUI editContext)
		{
			// We're only doing painting operations, early out if it's not a repaint
			if (Event.current.type != EventType.Repaint)
				return;

			if (editContext.hitValidTerrain)
			{
				BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.raycastHit.textureCoord, brushSize, 0.0f);
				PaintContext ctx = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds(), 1);
				TerrainPaintUtilityEditor.DrawBrushPreview(ctx, TerrainBrushPreviewMode.SourceRenderTexture, editContext.brushTexture, brushXform, GetDefaultBrushPreviewMaterial(isSelectionValid() ? new Color(0.5f, 0.5f, 1.0f, 1.0f) : new Color(1.0f, 0.5f, 0.5f, 1.0f)), 0);
				TerrainPaintUtility.ReleaseContextResources(ctx);
			}
		}

		public void PlaceRules(Terrain terrain, IOnPaint editContext)
		{
			if (terrain == null)
				return;

			Profiler.BeginSample("PlaceRules");

			// Plant a bunch of trees
			int nbRulesToRequest = GetRandomRuleNbRules();
			PaintRequest[] requests = new PaintRequest[nbRulesToRequest];
			Dictionary<Terrain, List<PaintRequest>> requestsPerTerrain = new Dictionary<Terrain, List<PaintRequest>>();
			for (int i = 0; i < requests.Length; i++)
			{
				var rule = GetRandomRule();

				Vector2 randomOffset = 0.5f * UnityEngine.Random.insideUnitCircle;
				randomOffset *= brushSize;
				var ruleWorldPosition = new Vector3(editContext.raycastHit.point.x + randomOffset.x, editContext.raycastHit.point.y, editContext.raycastHit.point.z + randomOffset.y);

				var intersectingTerrain = GetIntersectingTerrain(ruleWorldPosition);
				if (!requestsPerTerrain.ContainsKey(intersectingTerrain))
					requestsPerTerrain.Add(intersectingTerrain, new List<PaintRequest>());

				var uvPos = ScatteringRulesUtils.WorldToTerrain(ruleWorldPosition - intersectingTerrain.transform.position, intersectingTerrain.terrainData.size);
				var scaleHeight = UnityEngine.Random.Range(rule.minMaxHeight.x, rule.minMaxHeight.y);
				var scaleWidth = rule.lockWidthToHeight ? scaleHeight : UnityEngine.Random.Range(rule.minMaxWidth.x, rule.minMaxWidth.y);
				requestsPerTerrain[intersectingTerrain].Add(new PaintRequest(rule, uvPos, UnityEngine.Random.Range(0, 360), new Vector3(scaleWidth, scaleHeight, scaleWidth)));
			}

			//Get sub-rules
			List<ScatteringRule> rules = new List<ScatteringRule>();
			foreach (var rule in m_selectedRules)
				ScatteringRulesUtils.GatherRules(rule, ref rules, false);

			foreach (var t in requestsPerTerrain)
			{
				//make sure all our rules are also in the tree prototype array
				AddPrototypes(t.Key, rules.ToHashSet().ToArray());

				m_terrainExtensions[t.Key].Paint(editContext, t.Value.ToArray());
			}

			Profiler.EndSample();
		}

		Terrain GetIntersectingTerrain(Vector3 worldPos)
		{
			foreach (var t in m_terrainExtensions)
			{
				var terrainBounds = t.Value.terrainBounds;
				if (terrainBounds.Contains(worldPos))
					return t.Key;
			}

			return m_TargetTerrain;
		}

		Terrain[] GetIntersectingTerrains(Vector3 worldPos, float radius)
		{
			Bounds bounds = new Bounds(worldPos, new Vector3(radius * 2, float.PositiveInfinity, radius * 2));
			List<Terrain> intersectingTerrains = new List<Terrain>();
			foreach (var t in m_terrainExtensions)
			{
				var terrainBounds = t.Value.terrainBounds;
				if (terrainBounds.Intersects(bounds))
					intersectingTerrains.Add(t.Key);
			}

			return intersectingTerrains.ToArray();
		}

		private void RemoveRules(Terrain terrain, IOnPaint editContext, bool clearSelectedOnly)
		{
			Profiler.BeginSample("RemoveRules");
			var ruleWorldPosition = new Vector3(editContext.raycastHit.point.x, editContext.raycastHit.point.y, editContext.raycastHit.point.z);
			var radius = 0.5f * brushSize;
			//Find all Intersecting Terrains
			var intersectingTerrains = GetIntersectingTerrains(ruleWorldPosition, radius);

			foreach (var t in intersectingTerrains)
			{
				var uvPos = ScatteringRulesUtils.WorldToTerrain(ruleWorldPosition - t.transform.position, t.terrainData.size);
				float uvRadius = radius / t.terrainData.size.x;

				if (clearSelectedOnly)
				{
					List<ScatteringRule> rules = new List<ScatteringRule>();
					foreach (var rule in m_selectedRules)
						ScatteringRulesUtils.GatherRules(rule, ref rules, false);
					m_terrainExtensions[t].Erase(editContext, new Vector2(uvPos.x, uvPos.z), uvRadius, rules.ToHashSet().ToList());
				}
				else
					m_terrainExtensions[t].Erase(editContext, new Vector2(uvPos.x, uvPos.z), uvRadius);
			}

			Profiler.EndSample();
		}

		public int GetRandomRuleNbRules()
		{
			return Mathf.RoundToInt(brushSize * spacing); //That makes a lot of rules
		}

		public ScatteringRule GetRandomRule()
		{
			if (m_selectedRules == null || m_selectedRules.Count == 0)
				return null;

			var index = UnityEngine.Random.Range(0, m_selectedRules.Count);
			return m_selectedRules[index];
		}

		public bool isSelectionValid()
		{
			return m_selectedRules.Count > 0;
		}

		#region Prototypes
		private void AddPrototypes(Terrain terrain, ScatteringRule[] rulesUsed)
		{
			bool haveAddedSomething = false;
			if (rulesUsed != null && terrain.terrainData.treePrototypes != null)
			{
				//Check for treePrototypes
				{
					var listProto = terrain.terrainData.treePrototypes.ToList();
					foreach (var r in rulesUsed.Where(r => !r.isDetails))
					{
						bool isIncluded = false;
						foreach (var p in listProto.Where(p => p.prefab.name == r.name))
							isIncluded = true;

						if (!isIncluded)
						{
							var proto = new TreePrototype();
							proto.prefab = r.gameObject;
							listProto.Add(proto);
							haveAddedSomething = true;
						}
					}

					if (listProto.Count != terrain.terrainData.treePrototypes.Length)
						terrain.terrainData.treePrototypes = listProto.ToArray();
				}

				//Check for Details
				{
					var listProto = terrain.terrainData.detailPrototypes.ToList();
					foreach (var r in rulesUsed.Where(r => r.isDetails))
					{
						bool isIncluded = false;
						foreach (var p in listProto.Where(p => p.prototype.name == r.name))
							isIncluded = true;

						if (!isIncluded)
						{
							bool useInstancing = true;
							var proto = new DetailPrototype
							{
								prototype = r.gameObject,
								prototypeTexture = null,
								minWidth = 1,
								maxWidth = 2,
								minHeight = 1,
								maxHeight = 2,
								noiseSeed = 456813654,
								noiseSpread = 0.1f,
								holeEdgePadding = 0.0f / 100.0f,
								healthyColor = Color.white,
								dryColor = Color.white,
								renderMode = !useInstancing ? DetailRenderMode.Grass : DetailRenderMode.VertexLit,
								usePrototypeMesh = true,
								useInstancing = useInstancing,
							};
							listProto.Add(proto);
							haveAddedSomething = true;
						}
					}

					if (listProto.Count != terrain.terrainData.detailPrototypes.Length)
						terrain.terrainData.detailPrototypes = listProto.ToArray();
				}
			}

			if (haveAddedSomething)
			{
				m_terrainExtensions[terrain].OnExitToolMode();
				m_terrainExtensions[terrain].OnEnterToolMode();
			}
		}
		#endregion

		#region UI
		abstract class UIConstants
		{
			static public readonly int kPreviewSize = 50;
			static public readonly int kPreviewSpacing = 5;
			static public readonly int kToggleSize = 25;
			static public readonly int kToggleSpacing = 5;

			static public readonly GUIContent toolName = EditorGUIUtility.TrTextContent("Paint Rules");
			static public readonly GUIContent toolDescription = EditorGUIUtility.TrTextContent("Click to paint rules. \n\nHold ctrl and click to erase rules. \n\nHold shift and click to erase only rules of the selected type");
			static public readonly GUIContent brushSize = EditorGUIUtility.TrTextContent("Brush Size", "Size of the brush used to paint.");
			static public readonly GUIContent settings = EditorGUIUtility.TrTextContent("Settings");
			static public readonly GUIContent treeDensityLabel = EditorGUIUtility.TrTextContent("Tree Density");
			static public readonly GUIContent treeDensityTooltip = EditorGUIUtility.TrTextContent("How dense trees are you painting (number of trees per square meter)");
			static public readonly GUIContent missingSelection = EditorGUIUtility.TrTextContent("Please select at least one rule to paint");
			static public readonly GUIContent rulesLabel = EditorGUIUtility.TrTextContent("Rules");
			static public readonly GUIContent detailMeshLabel = EditorGUIUtility.TrTextContent("Detail Mesh");

			static public readonly string rulesPropertyName = "rules";
			static public readonly string prefabPropertyName = "prefab";
			static public readonly string isSelectedPropertyName = "isSelected";
		}

		[SerializeField]
		public ReorderableList rlist = null;

		public override void OnInspectorGUI(Terrain terrain, IOnInspectorGUI editContext)
		{
			if (needUpdate)
			{
				m_terrainExtensions[terrain].OnExitToolMode();
				m_terrainExtensions[terrain].OnEnterToolMode();
				needUpdate = false;
			}

			DisplayBrushUI();

			var nbExclusionZones = 0;
			//Each extension have the list of all the zones, so we only need one to the the number of zones
			foreach (var ext in m_terrainExtensions.Where(ext => ext.Value.isValid))
			{
				nbExclusionZones += ext.Value.exclusionZoneCount;
				break;
			}
			if (nbExclusionZones > 0 && GUILayout.Button("Update Exclusions"))
			{
				foreach (var ext in m_terrainExtensions)
					ext.Value.UpdateExclusionZones();
			}


			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Update Splatmaps"))
			{
				foreach (var ext in m_terrainExtensions)
					ext.Value.UpdateSplatmaps();
			}
			if (GUILayout.Button("Clear Splatmaps"))
			{
				foreach (var ext in m_terrainExtensions)
					ext.Value.ClearSplatmaps();
			}
			GUILayout.EndHorizontal();

			DisplayRulesUI();
		}

		public void DisplayBrushUI()
		{
			if (m_TargetTerrain == null)
				return;

			GUILayout.Label(UIConstants.settings, EditorStyles.boldLabel);

			// Placement distance
			brushSize = PowerSlider(UIConstants.brushSize.text, UIConstants.brushSize, brushSize, 1, Mathf.Min(m_TargetTerrain.terrainData.size.x, m_TargetTerrain.terrainData.size.z), 4.0f);
			spacing = EditorGUILayout.Slider(EditorGUIUtility.TrTextContent(UIConstants.treeDensityLabel.text, UIConstants.treeDensityTooltip.text), spacing, 0, 2.0f);

			GUILayout.Space(5);
		}

		void DisplayRulesUI()
		{
			if (m_rulesObject == null)
				return;

			if (!isSelectionValid())
			{
				var oldColor = GUI.contentColor;
				GUI.contentColor = new Color(1.0f, 0.5f, 0.5f, 1.0f);
				EditorGUILayout.LabelField(UIConstants.missingSelection);
				GUI.contentColor = oldColor;
			}

			m_rulesObject.Update();

			EditorGUI.BeginChangeCheck();

			if (rlist == null)
				rlist = new ReorderableList(m_rulesObject, m_rulesObject.FindProperty(UIConstants.rulesPropertyName), true, true, true, true);

			rlist.multiSelect = true;
			rlist.draggable = true;
			rlist.elementHeight = 50;
			rlist.drawHeaderCallback = (Rect rect) =>
			{
				EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), UIConstants.rulesLabel);

				if (rect.Contains(Event.current.mousePosition))
				{
					bool HasValidElements = false;
					foreach (var or in DragAndDrop.objectReferences)
					{
						if ((or as GameObject)?.GetComponent<ScatteringRule>() != null)
						{
							HasValidElements = true;
							break;
						}
					}

					if (Event.current.type == EventType.DragUpdated && HasValidElements)
					{
						DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
						Event.current.Use();
					}
					else if (Event.current.type == EventType.DragPerform && HasValidElements)
					{
						foreach (var or in DragAndDrop.objectReferences)
						{
							if ((or as GameObject)?.GetComponent<ScatteringRule>() != null)
							{
								var index = rlist.serializedProperty.arraySize == 0 ? 0 : rlist.serializedProperty.arraySize - 1;

								rlist.serializedProperty.InsertArrayElementAtIndex(index);
								var elem = rlist.serializedProperty.GetArrayElementAtIndex(index);
								elem.FindPropertyRelative(UIConstants.prefabPropertyName).objectReferenceValue = or;
							}
						}
						rlist.serializedProperty.serializedObject.ApplyModifiedProperties();
						Event.current.Use();
					}

				}
			};
			rlist.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
			{
				if (rect.Contains(Event.current.mousePosition))
				{
					bool HasValidElements = false;
					foreach (var or in DragAndDrop.objectReferences)
					{
						if ((or as GameObject)?.GetComponent<ScatteringRule>() != null)
						{
							HasValidElements = true;
							break;
						}
					}

					if (Event.current.type == EventType.DragUpdated && HasValidElements)
					{
						DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
						Event.current.Use();
					}
					else if (Event.current.type == EventType.DragPerform && HasValidElements)
					{
						foreach (var or in DragAndDrop.objectReferences)
						{
							if ((or as GameObject)?.GetComponent<ScatteringRule>() != null)
							{
								var lelement = rlist.serializedProperty.GetArrayElementAtIndex(index);
								lelement.FindPropertyRelative(UIConstants.prefabPropertyName).objectReferenceValue = or;

								lelement.serializedObject.ApplyModifiedProperties();
								needUpdate = true;
								break;
							}
						}
						Event.current.Use();
					}
				}

				var element = rlist.serializedProperty.GetArrayElementAtIndex(index);
				if (element == null) return;
				var prefabProp = element.FindPropertyRelative(UIConstants.prefabPropertyName);
				if (prefabProp == null) return;
				var isSelectedProp = element.FindPropertyRelative(UIConstants.isSelectedPropertyName);
				if (isSelectedProp == null) return;

				EditorGUI.BeginProperty(rect, new GUIContent(""), element);

				isSelectedProp.boolValue = isActive;

				//Preview
				var previewRect = new Rect(rect.x, rect.y, UIConstants.kPreviewSize, UIConstants.kPreviewSize);
				var objectRect = new Rect(previewRect.x + previewRect.width + UIConstants.kPreviewSpacing, rect.y, rect.width - previewRect.width - UIConstants.kPreviewSpacing - UIConstants.kToggleSize - UIConstants.kToggleSpacing, EditorGUIUtility.singleLineHeight);
				var detailRect = new Rect(objectRect.x, objectRect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, objectRect.width, objectRect.height);
				var toggleRect = new Rect(objectRect.x + objectRect.width + UIConstants.kToggleSpacing, objectRect.y, UIConstants.kToggleSize, objectRect.height);

				EditorGUI.ObjectField(objectRect, prefabProp, typeof(ScatteringRule), new GUIContent(""));
				EditorGUI.PropertyField(toggleRect, isSelectedProp, new GUIContent(""), false);

				var prefabReference = (MonoBehaviour)prefabProp.objectReferenceValue;
				if (prefabReference != null)
				{
					var texture = AssetPreview.GetAssetPreview(prefabReference.gameObject);
					if (texture != null)
						EditorGUI.DrawPreviewTexture(previewRect, texture);

					ScatteringRule comp = prefabReference.GetComponent<ScatteringRule>();
					if (comp != null && comp.isDetails)
						EditorGUI.LabelField(detailRect, new GUIContent(UIConstants.detailMeshLabel));
				}

				EditorGUI.EndProperty();
			};
			rlist.onAddCallback = (ReorderableList list) =>
			{
				ReorderableList.defaultBehaviours.DoAddButton(list);
				needUpdate = true;
			};
			rlist.onChangedCallback = (ReorderableList list) =>
			{
				needUpdate = true;
			};
			rlist.DoLayoutList();

			if (EditorGUI.EndChangeCheck())
			{
				//Update terrain prototypes based on our selections maybe
			}


			m_rulesObject.ApplyModifiedProperties();

			//Update our selection
			m_selectedRules.Clear();
			for (int i = 0; i < m_rules.rules.Count; i++)
			{
				if (m_rules.rules[i].isSelected)
					m_selectedRules.Add(m_rules.rules[i].prefab);
				//GatherRules(m_rules.rules[i].prefab, ref m_selectedRules);
			}
		}

		static float ScaledSliderWithRounding(GUIContent content, float valueInPercent, float minVal, float maxVal, float scale, float precision)
		{
			EditorGUI.BeginChangeCheck();

			minVal *= scale;
			maxVal *= scale;
			float v = Mathf.Round(valueInPercent * scale / precision) * precision;
			v = Mathf.Clamp(v, minVal, maxVal);   // this keeps the slider knob from disappearing
			v = EditorGUILayout.Slider(content, v, minVal, maxVal);

			if (EditorGUI.EndChangeCheck())
			{
				return v / scale;
			}
			return valueInPercent;
		}

		static float PowerSlider(string label, GUIContent content, float value, float minVal, float maxVal, float power, GUILayoutOption[] options = null)
		{
			return EditorGUILayout.Slider(label, Mathf.Clamp(value, minVal, maxVal), minVal, maxVal, options);
		}

		private void OnValidate()
		{
			OnExitToolMode();
			OnEnterToolMode();
		}
		#endregion
	}
}