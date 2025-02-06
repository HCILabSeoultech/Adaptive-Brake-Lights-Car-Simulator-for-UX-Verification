using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.TerrainTools;
using System.IO;
using System;
using com.unity.testtrack.terrainsystem;
using com.unity.testtrack.physics;

namespace com.unity.testtrack.terrainsystem.editor
{
	public class MiscTools : Editor
	{
		[MenuItem("GameObject/Terrain/Add Rules Components #&p", false, 0)]
		private static void AddRulesComponents(MenuCommand command)
		{
			var go = command.context as GameObject;
			if (go != null)
			{
				foreach (var terrain in go.GetComponentsInChildren<Terrain>())
				{
					{
						if (!terrain.TryGetComponent<ScatteringRules>(out var component))
							terrain.gameObject.AddComponent<ScatteringRules>();
					}

					{
						if (!terrain.TryGetComponent<ScatteringRulesBridge>(out var component))
							terrain.gameObject.AddComponent<ScatteringRulesBridge>();
					}

					{
						if (!terrain.TryGetComponent<TreePrototypeExtensionDataProvider>(out var component))
							terrain.gameObject.AddComponent<TreePrototypeExtensionDataProvider>();
					}

					{
						if (!terrain.TryGetComponent<DetailPrototypeExtensionDataProvider>(out var component))
							terrain.gameObject.AddComponent<DetailPrototypeExtensionDataProvider>();
					}

					{
						SplatmapUpdateManager updateManager = null;
						if (!terrain.TryGetComponent<SplatmapUpdateManager>(out updateManager))
							updateManager = terrain.gameObject.AddComponent<SplatmapUpdateManager>();

						updateManager.m_basedAlphaMaps.Clear();

						SplatmapUpdateManager.TerainAlphaMapData alphaMapData = new SplatmapUpdateManager.TerainAlphaMapData();
						alphaMapData.terrain = terrain;
						updateManager.m_basedAlphaMaps.Add(alphaMapData);
					}

					{
						if (!terrain.TryGetComponent<TerrainPhysicalMaterialExtension>(out var component))
							terrain.gameObject.AddComponent<TerrainPhysicalMaterialExtension>();
					}
				}
			}
		}

		[MenuItem("GameObject/Terrain/Add Rules Components #&p", true, 0)]
		private static bool CanAddRulesComponents()
		{
			foreach (var go in Selection.gameObjects.Where(go => go != null && go.GetComponentsInChildren<Terrain>().Length > 0))
				return true;

			return false;
		}

		[MenuItem("GameObject/Terrain/Copy rules to other terrains #&p", false, 0)]
		private static void CopyRulesComponents(MenuCommand command)
		{
			var go = command.context as GameObject;
			if (go != null && go.TryGetComponent<ScatteringRules>(out var currentComponent))
			{
				var rules = FindObjectsOfType<ScatteringRules>();
				foreach (var rule in rules.Where(rule => rule != currentComponent))
				{
					rule.rules.Clear();
					rule.rules.AddRange(currentComponent.rules);
				}
			}
		}

		[MenuItem("GameObject/Terrain/Copy rules to other terrains #&p", true, 0)]
		private static bool CanCopyRulesComponents()
		{
			foreach (var go in Selection.gameObjects.Where(go => go != null && go.GetComponentsInChildren<Terrain>().Length > 0))
				return true;

			return false;
		}

		public static RemoveTilesWizard removeTileWizard = null;
		[MenuItem("GameObject/Terrain/Remove terrain outside bounding box #&p", false, 0)]
		private static void RemoveUnuseTiles()
		{
			if (removeTileWizard == null)
				removeTileWizard = ScriptableWizard.DisplayWizard<RemoveTilesWizard>("Remove Tiles Settings", "Apply");
		}

		[MenuItem("GameObject/Terrain/Remove terrain outside bounding box #&p", true, 0)]
		private static bool CanRemoveUnuseTiles()
		{
			foreach (var go in Selection.gameObjects.Where(go => go != null && go.GetComponentsInChildren<Terrain>().Length > 0))
				return true;
			return false;
		}

		[MenuItem("GameObject/Terrain/Compute Visibility Data #&p", false, 0)]
		private static void ComputeVisibilityData(MenuCommand command)
		{
			var go = command.context as GameObject;
			if (go != null)
			{
				TerrainVisibilityData visData;
				if (!go.TryGetComponent(out visData))
					visData = go.AddComponent<TerrainVisibilityData>();

				visData.BuildVisibility();
			}
		}

		[MenuItem("GameObject/Terrain/Compute Visibility Data #&p", true, 0)]
		private static bool CanComputeVisibilityData()
		{
			foreach (var go in Selection.gameObjects.Where(go => go != null && go.GetComponentsInChildren<Terrain>().Length > 0))
				return true;

			return false;
		}

		[MenuItem("GameObject/Terrain/Clear Visibility Data #&p", false, 0)]
		private static void ClearVisibilityData(MenuCommand command)
		{
			var go = command.context as GameObject;
			if (go != null)
			{
				TerrainVisibilityData visData;
				if (!go.TryGetComponent(out visData))
					visData = go.AddComponent<TerrainVisibilityData>();

				visData.ClearVisibility();
			}
		}

		[MenuItem("GameObject/Terrain/Clear Visibility Data #&p", true, 0)]
		private static bool CanClearVisibilityData()
		{
			foreach (var go in Selection.gameObjects.Where(go => go != null && go.GetComponentsInChildren<Terrain>().Length > 0))
				return true;

			return false;
		}
	}
}

