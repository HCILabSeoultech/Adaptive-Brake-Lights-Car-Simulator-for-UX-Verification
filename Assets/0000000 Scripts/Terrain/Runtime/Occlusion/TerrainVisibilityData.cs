using com.unity.testtrack.utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.unity.testtrack.terrainsystem
{
	public class TerrainVisibilityData : MonoBehaviour
	{
		[Tooltip("Do we check the visibility of each corners of the tiles when computing the visibility data or just the center?")]
		[SerializeField] bool				m_checkBoundingCorners = false;
		[Tooltip("Do we check if all tiles around a disable tile are visible, if they are we enable the disable tile")]
		[SerializeField] bool				m_checkNeighbors = false;
		[Tooltip("A list of tiles we want to force to load")]
		[SerializeField] List<Terrain>		m_forcedEnableTile = new List<Terrain>();
		[ReadOnly]
		[SerializeField] List<Terrain>		m_visibleTiles = new List<Terrain>();
		[Tooltip("A list of tiles we want to force to unload")]
		[SerializeField] List<Terrain>		m_forcedDisableTile = new List<Terrain>();
		[ReadOnly]
		[SerializeField] List<Terrain>		m_disableTiles = new List<Terrain>();
#if UNITY_EDITOR
		[Tooltip("Display debug information?")]
		[SerializeField] bool				m_drawDebug = false;
#endif


		public List<Terrain> disableTiles => m_disableTiles;
		public List<Terrain> forcedDisableTile => m_forcedDisableTile;
		public List<Terrain> visibleTiles => m_visibleTiles;
		public List<Terrain> forcedEnableTile => m_forcedEnableTile;

		[ContextMenu("Build Visibility")]
		public void BuildVisibility()
		{
			m_visibleTiles.Clear();
			m_disableTiles.Clear();

			var allTiles = FindObjectsOfType<Terrain>(true).ToList();
			if (allTiles.Count == 0)
				return;

			if (!gameObject.ComputeBounds(out var objectBounds, true))
				return;

			var srcPosition = new Vector3(objectBounds.center.x, objectBounds.max.y, objectBounds.center.z);
			Vector3[] srcPositions = {
				new Vector3(objectBounds.center.x, objectBounds.max.y, objectBounds.center.z), //Center
				objectBounds.max, //Top corners
				new Vector3(objectBounds.min.x, objectBounds.max.y, objectBounds.min.z),
				new Vector3(objectBounds.min.x, objectBounds.max.y, objectBounds.max.z),
				new Vector3(objectBounds.max.x, objectBounds.max.y, objectBounds.min.z),
			};

			int layerMask = LayerMask.GetMask(new string[] { "TerrainTileVisibiliyBlocker" });
			foreach (var tile in allTiles)
			{
				if (tile == this.GetComponent<Terrain>())
					continue;

				if (!tile.gameObject.ComputeBounds(out var tileBounds, true))
				{
					m_disableTiles.Add(tile);
					continue;
				}

				tileBounds = new Bounds(tileBounds.center, tileBounds.size + (Vector3.one * -1.0f));

				Vector3[] dstPositions = {
					new Vector3(tileBounds.center.x, tileBounds.max.y, tileBounds.center.z), //Center
					tileBounds.max, //Top corners
					new Vector3(tileBounds.min.x, tileBounds.max.y, tileBounds.min.z),
					new Vector3(tileBounds.min.x, tileBounds.max.y, tileBounds.max.z),
					new Vector3(tileBounds.max.x, tileBounds.max.y, tileBounds.min.z),
				};

				bool isVisible = IsAnyPointsVisible(GetComponent<Terrain>(), tile, srcPositions, dstPositions, layerMask);
				if (isVisible)
					m_visibleTiles.Add(tile);
				else
					m_disableTiles.Add(tile);
			}

			if (m_checkNeighbors)
			{
				foreach (var tile in allTiles.Where(tile => m_disableTiles.Contains(tile)))
				{
					var neighbors = GetNeighbors(tile, allTiles);
					var allVisibleNeighbors = neighbors.FindAll(item => m_visibleTiles.Contains(item));
					if (allVisibleNeighbors.Count == neighbors.Count)
					{
						//Tile should be visible
						m_disableTiles.Remove(tile);
						m_visibleTiles.Add(tile);
					}
				}
			}
		}

		List<Terrain> GetNeighbors(Terrain tile, List<Terrain> allTiles)
		{
			List<Terrain> neighbors = new List<Terrain>();
			if (tile.gameObject.ComputeBounds(out var objectBounds, true))
			{
				objectBounds.Expand(20);
				foreach (var t in allTiles.Where(t => t.gameObject != tile.gameObject))
				{
					if (t.gameObject.ComputeBounds(out var tileBounds, true))
					{
						tileBounds = new Bounds(tileBounds.center, new Vector3(tileBounds.size.x, float.MaxValue, tileBounds.size.z));
						if (objectBounds.Intersects(tileBounds))
							neighbors.Add(t);
					}
				}
			}

			return neighbors;
		}

		bool IsAnyPointsVisible(Terrain srctile, Terrain dsttile, Vector3[] srcPos, Vector3[] dstPos, int layerMask)
		{
			int srcCount = m_checkBoundingCorners ? srcPos.Length : 1;
			int dstCount = m_checkBoundingCorners ? dstPos.Length : 1;
			for (int i = 0; i < srcCount; i++)
			{
				for (int j = 0; j < dstCount; j++)
				{
					//Check each corner
					if (IsDirectlyVisible(srctile, dsttile, srcPos[i], dstPos[j], layerMask))
						return true;
				}
			}

			return false;
		}

		bool IsDirectlyVisible(Terrain srctile, Terrain dsttile, Vector3 src, Vector3 dst, int layerMask)
		{
			var diff = dst - src;
			Ray ray = new Ray(src, diff);
			if (Physics.Raycast(ray, out var hitinfo, diff.magnitude, layerMask))
				return !(hitinfo.collider.gameObject != srctile.gameObject && hitinfo.collider.gameObject != dsttile.gameObject);

			return true;
		}

		[ContextMenu("Clear Visibility Data")]
		public void ClearVisibility()
		{
			m_visibleTiles.Clear();
			m_disableTiles.Clear();
			m_forcedEnableTile.Clear();
			m_forcedDisableTile.Clear();
		}

		private void OnDrawGizmosSelected()
		{
#if UNITY_EDITOR
			if (!m_drawDebug || Selection.activeGameObject != gameObject)
				return;

			var visibleTiles = m_visibleTiles;
			visibleTiles.AddRange(m_forcedEnableTile);
			foreach (var tile in visibleTiles)
			{
				Gizmos.color = Color.green;
				if (tile.gameObject.ComputeBounds(out var bounds, true))
				{
					Gizmos.DrawWireCube(bounds.center, bounds.size);
				}
			}

			var disableTiles = m_disableTiles;
			disableTiles.AddRange(m_forcedDisableTile);
			foreach (var tile in disableTiles.Except(m_forcedEnableTile))
			{
				if (tile.gameObject.ComputeBounds(out var bounds, true))
				{
					Gizmos.color = Color.red;
					Gizmos.DrawWireCube(bounds.center, bounds.size);

					Vector3[] dstPositions = {
					new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), //Center
					bounds.max, //Top corners
					new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
					new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
					new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
				};

					Gizmos.color = Color.white;
					foreach (var pos in dstPositions)
						Gizmos.DrawSphere(pos, 1.0f);
				}
			}
#endif
		}
	}
}
