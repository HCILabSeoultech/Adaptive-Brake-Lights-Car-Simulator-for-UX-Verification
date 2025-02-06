using com.unity.testtrack.Data;
using com.unity.testtrack.physics;
using System.Linq;
using UnityEngine;
using VolvoCars.ActorManagement.Car.VehicleDynamics;

namespace com.unity.testtrack.vehicle
{
    public class WheelsCollidingPhysicalMaterialsUpdater : MonoBehaviour
    {
        [SerializeField] private CollidingPhysicalMaterial m_physicalProperties;
        [SerializeField] private ChassisDynamics m_chassisDynamics;

        private ChassisDynamics.Wheel m_wheelFL;
        private ChassisDynamics.Wheel m_wheelFR;
        private ChassisDynamics.Wheel m_wheelRL;
        private ChassisDynamics.Wheel m_wheelRR;

        // Start is called before the first frame update
        void Start()
        {
            if (m_chassisDynamics == null)
                m_chassisDynamics = GetComponent<ChassisDynamics>();

            m_wheelFL = m_chassisDynamics.wheelFL;
            m_wheelFR = m_chassisDynamics.wheelFR;
            m_wheelRL = m_chassisDynamics.wheelRL;
            m_wheelRR = m_chassisDynamics.wheelRR;
        }

        private void FixedUpdate()
        {
            if (m_chassisDynamics == null)
                return;

            var physMaterials = m_physicalProperties.Value;

            physMaterials.fL = GetWheelColliderPhysicalProperties(m_wheelFL.collider);
            physMaterials.fR = GetWheelColliderPhysicalProperties(m_wheelFR.collider);
            physMaterials.rL = GetWheelColliderPhysicalProperties(m_wheelRL.collider);
            physMaterials.rR = GetWheelColliderPhysicalProperties(m_wheelRR.collider);

            m_physicalProperties.SetValue(physMaterials);
        }

        PhysicalProperty GetWheelColliderPhysicalProperties(WheelCollider wheelCollider)
        {
            if (wheelCollider != null && wheelCollider.GetGroundHit(out var hit))
                return ExtractMaterial(hit.collider, hit.point);

            return null;
        }

        PhysicalProperty ExtractMaterial(Collider col, Vector3 hitLocation)
        {
            if (col == null)
                return null;

            if (col is TerrainCollider)
            {
                var terrain = GetTerrain((col as TerrainCollider).terrainData);
                return ExtractDominantTerrainLayer(terrain, hitLocation);
            }

            return ExtractPhysicalProperties(col.gameObject, hitLocation);
        }

        /// <summary>
        /// Extract and return the Physical Property of the dominant layer on a terrain at a specific location
        /// </summary>
        /// <param name="terrain"></param>
        /// <param name="hitLocation"></param>
        /// <returns>The dominant physical property at a specific location</returns>
        PhysicalProperty ExtractDominantTerrainLayer(Terrain terrain, Vector3 hitLocation)
        {
            if (terrain == null)
                return null;

            //Convert hit location to terrain location
            var pos = hitLocation - terrain.GetPosition();
            pos.Set(pos.x / terrain.terrainData.size.x, pos.y / terrain.terrainData.size.y, pos.z / terrain.terrainData.size.z);

            var alphamaps = terrain.terrainData.GetAlphamaps((int)(pos.x * terrain.terrainData.alphamapWidth), (int)(pos.z * terrain.terrainData.alphamapHeight), 1, 1);
            if (alphamaps != null && GetDominantLayerIndex(alphamaps, out var dominantLayerIndex, out var dominantLayerContribution))
            {
                var layer = terrain.terrainData.terrainLayers[dominantLayerIndex];
                var tPhysicalExt = GetTerrainPhysicalExtension(terrain);
                if (tPhysicalExt?._physicalProperties != null && layer != null)
                {
                    foreach (TerrainLayerPhysicalProperty layerProp in tPhysicalExt._physicalProperties.Where(layerProp => (layerProp as TerrainLayerPhysicalProperty)?.m_layerName == layer.name))
                        return layerProp;
                }
            }

            return null;
        }

        TerrainPhysicalMaterialExtension GetTerrainPhysicalExtension(Terrain terrain)
        {
            return terrain?.GetComponent<TerrainPhysicalMaterialExtension>();
        }

        Terrain GetTerrain(TerrainData terrainData)
        {
            foreach (var t in Terrain.activeTerrains.Where(t => t.terrainData == terrainData))
                return t;

            return null;
        }

        /// <summary>
        /// Find the dominant layer based on the terrain alpha maps contributions
        /// </summary>
        /// <param name="alphamaps"></param>
        /// <param name="layerIndex"></param>
        /// <param name="layerContribution"></param>
        /// <returns>false if no dominant index was found</returns>
        bool GetDominantLayerIndex(float[,,] alphamaps, out int layerIndex, out float layerContribution)
        {
            layerIndex = -1;
            layerContribution = -1;
            for (int layer = 0; layer < alphamaps.GetLength(2); layer++)
            {
                if (alphamaps[0, 0, layer] > layerContribution)
                {
                    layerIndex = layer;
                    layerContribution = alphamaps[0, 0, layer];

                    //If the layer compose more than 50% of the ground early escape
                    if (layerContribution > 0.5f)
                        break;
                }
            }

            return layerIndex != -1;
        }

        PhysicalProperty ExtractPhysicalProperties(GameObject go, Vector3 hitLocation)
        {
            if (go != null)
            {
                var ext = go.GetComponent<PhysicalMaterialExtension>();
                if (ext != null && ext._physicalProperties != null && ext._physicalProperties.Count > 0)
                    return ext._physicalProperties[0]; //Non-Terrain Game Objects can only have 1 physical property
            }

            return null;
        }
    }
}
