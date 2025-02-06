using UnityEngine;

namespace com.unity.testtrack.physics
{
	[CreateAssetMenu(fileName = "TerrainLayerPhysicalProperty", menuName = "ScriptableObjects/Materials/CreateTerrainLayerPhysicalProperty", order = 1)]
    public class TerrainLayerPhysicalProperty : PhysicalProperty
    {
        public string m_layerName;
    }
}