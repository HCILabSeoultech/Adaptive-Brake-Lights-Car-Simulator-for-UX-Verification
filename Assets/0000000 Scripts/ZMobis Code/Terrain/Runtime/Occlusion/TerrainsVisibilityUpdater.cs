using com.unity.testtrack.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
    public class TerrainsVisibilityUpdater : MonoBehaviour
    {
        [Tooltip("Does the component affect the terrain tree drawing flag?")]
        [SerializeField] bool   m_TreeDrawing = true;
        [Tooltip("Does the component affect the terrain heightfield drawing flag?")]
        [SerializeField] bool   m_HeightfieldDrawing = true;
        [Tooltip("Does the component affect the terrain GameObject visibility flag?")]
        [SerializeField] bool   m_GameObjectVisibility = true;
        [Tooltip("Are we using the baked visibility data or on the frustum visibility?")]
        [SerializeField] bool m_IgnoreBakedVisibilityData = false;
        [Tooltip("Maximum number of trees on screen at the same time, per quality level. 0 = infinity")]
        [SerializeField] List<int> m_maxTrees = new List<int>() { 0, 10000, 10000 };
        [Tooltip("Display debug information")]
        [SerializeField] bool   m_drawDebug = false;

        Terrain[]                   m_tiles; //All the terrains in the scene that has visibility data
        TerrainVisibilityData[]     m_tilesVisData; //All the visiblity data 
        List<Terrain>               m_currentVisibleTiles = new List<Terrain>();
        int                         m_currentIndex = -1;

        // Start is called before the first frame update
        void Start()
        {
            m_tilesVisData  = FindObjectsOfType<TerrainVisibilityData>(true);
            m_tiles         = Array.ConvertAll(m_tilesVisData, item => item.GetComponent<Terrain>());
            m_currentIndex  = -1;
        }

        // Update is called once per frame
        void Update()
        {
            if (m_tiles == null || m_tiles.Length == 0)
                return;

            SetVisibility(true);

            m_currentIndex = FindTileIndexContaining(new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z));
            if (m_currentIndex == -1)
                return;

            UpdateVisibilty(m_currentIndex);
        }

        void UpdateVisibilty(int currentTileIndex)
        {
            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
            m_currentVisibleTiles.Clear();

            if (!m_IgnoreBakedVisibilityData)
            {
                var tileVisData = m_tilesVisData[currentTileIndex];
                var disableTiles = tileVisData.disableTiles.ToList();
                disableTiles.AddRange(tileVisData.forcedDisableTile);
                foreach (var tile in disableTiles)
                    SetTileVisibility(tile, false);

                var visibleTiles = tileVisData.visibleTiles.ToList();
                visibleTiles.AddRange(tileVisData.forcedEnableTile);
                foreach (var tile in visibleTiles)
                {
                    var bounds = new Bounds(tile.terrainData.bounds.center + tile.GetPosition(), tile.terrainData.bounds.size);
                    var visible = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);

                    SetTileVisibility(tile, visible);
                    if (visible)
                        m_currentVisibleTiles.Add(tile);
                }
            }
			else
			{
                foreach (var tile in m_tiles)
                {
                    var bounds = new Bounds(tile.terrainData.bounds.center + tile.GetPosition(), tile.terrainData.bounds.size);
                    var visible = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
                    SetTileVisibility(tile, visible);
                    if (visible)
                        m_currentVisibleTiles.Add(tile);
                }
            }

			int currentQualityLevel = QualitySettings.GetQualityLevel();
			if (currentQualityLevel < m_maxTrees.Count() && m_maxTrees[currentQualityLevel] > 0)
			{
				m_currentVisibleTiles.Sort((a, b) => (Camera.main.transform.position - a.GetPosition()).sqrMagnitude.CompareTo((Camera.main.transform.position - b.GetPosition()).sqrMagnitude));
				int count = 0;
				foreach (var tile in m_currentVisibleTiles)
				{
					count += tile.terrainData.treeInstanceCount;
					if (count > m_maxTrees[currentQualityLevel])
						SetTileVisibility(tile, false, true);
				}
			}
		}

        void SetVisibility(bool visible)
        {
            foreach (var tile in m_tiles.Where(tile => tile != null))
                SetTileVisibility(tile, visible);
        }

        void SetTileVisibility(Terrain tile, bool visible, bool affectOnlyTreeVisibility = false)
        {
            if (tile.drawTreesAndFoliage != visible && m_TreeDrawing)
                tile.drawTreesAndFoliage = visible;
            if (tile.drawHeightmap != visible && m_HeightfieldDrawing && !affectOnlyTreeVisibility)
                tile.drawHeightmap = visible;

            if (m_GameObjectVisibility && tile.gameObject.activeSelf != visible && !affectOnlyTreeVisibility)
                tile.gameObject.SetActive(visible);
        }

        int FindTileIndexContaining(Vector2 pos2D)
        {
            for (int i = 0; m_tiles != null && i < m_tiles.Length; i++)
            {
                var tile            = m_tiles[i];
                var tileWorldPos    = tile.terrainData.bounds.min + tile.GetPosition();

                Rect rect = new Rect(tileWorldPos.x, tileWorldPos.z, tile.terrainData.bounds.size.x, tile.terrainData.bounds.size.z);
                if (rect.Contains(pos2D))
                    return i;
            }

            return -1;
        }

        private void OnDrawGizmos()
        {
            if (!m_drawDebug || m_tilesVisData == null ||
                m_currentIndex < 0 || m_currentIndex >= m_tilesVisData.Length)
                return;

            var tileVisData = m_tilesVisData[m_currentIndex];
            if (tileVisData.GetComponent<Terrain>().gameObject.ComputeBounds(out var bounds, true))
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }

        }

        [ContextMenu("Build Visibility")]
        void BuildVisibility()
        {
            m_tilesVisData = FindObjectsOfType<TerrainVisibilityData>(true);
            if (m_tilesVisData != null)
            {
                foreach (var visData in m_tilesVisData)
                    visData.BuildVisibility();
            }
        }

        [ContextMenu("Clear Visibility Data")]
        void ClearVisibility()
        {
            m_tilesVisData = FindObjectsOfType<TerrainVisibilityData>(true);
            if (m_tilesVisData != null)
            {
                foreach (var visData in m_tilesVisData)
                    visData.ClearVisibility();
            }
        }
    }
}
