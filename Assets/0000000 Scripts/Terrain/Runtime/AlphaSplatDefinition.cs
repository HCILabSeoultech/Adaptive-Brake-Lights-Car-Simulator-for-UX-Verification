using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	[CreateAssetMenu(fileName = "AlphaSplatDefinition", menuName = "ScriptableObjects/Create Alpha Splat Definition", order = 1)]
	public class AlphaSplatDefinition : ScriptableObject
	{
		[System.Serializable]
		public class layerContribution
		{
			public string name;

			[Range(0, 1)]
			public float contribution;
		}

		[SerializeField]
		public List<layerContribution> m_contributions = new List<layerContribution>();


		public float GetContribution(string layerName)
		{
			foreach (var l in m_contributions.Where(l => l.name == layerName))
			{
				return l.contribution;
			}

			return 0;
		}

		public int GetContributionIndex(TerrainData terrainData, string layerName)
		{
			var layers = terrainData.terrainLayers;
			for (int i = 0; i < layers.Length; i++)
			{
				if (layers[i].name == layerName)
					return i;
			}
			return -1;
		}

		static public Color[] GetAlphaMapsContributions(AlphaSplatDefinition def, TerrainData terrainData)
		{
			Color[] colors = new Color[terrainData.alphamapTextureCount];
			if (colors.Length > 0)
			{
				for (int i = 0; i < colors.Length; i++) //Default to layer 0 = full opaque
					colors[i] = new Color(0, 0, 0, 0);

				//Get the layers for each definitions
				if (def != null)
				{
					foreach (var c in def.m_contributions)
					{
						var index = def.GetContributionIndex(terrainData, c.name);
						if (index == -1)
							continue;

						int alphaMapIndex = (index / 4);
						int colorIndex = (index % 4);

						colors[alphaMapIndex][colorIndex] = c.contribution;
					}
				}
			}

			return colors;
		}
	}
}
