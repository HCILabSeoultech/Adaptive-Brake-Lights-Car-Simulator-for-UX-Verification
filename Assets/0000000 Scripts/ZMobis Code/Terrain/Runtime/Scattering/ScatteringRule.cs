using System.Collections.Generic;
using UnityEngine;
using com.unity.testtrack.terrainsystem.attributes;
using com.unity.testtrack.terrainsystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.unity.testtrack.terrainsystem
{
	[ExecuteAlways]
	public class ScatteringRule : MonoBehaviour
	{
		#region ScatteringObjectDef
		public class ScatteringObjectDef
		{
			public GameObject prefab;
			public string prefabName;
			public Vector3 localPosition; //Relative to 0,0,0
			public Vector3 position; //World Position including the transforms
			public bool isDetails;

			public ScatteringObjectDef(CircularScatteringRule rule, int prefabIndex, Vector3 rootPosition, Vector3 scale)
			{
				prefab = rule.prefabs[prefabIndex].prefab;
				prefabName = prefab.name;
				localPosition = rule.GetDonutRandomPosition(scale);
				position = rootPosition + localPosition;

				var comp = prefab.GetComponent<ScatteringRule>();
				isDetails = comp != null && comp.isDetails;
			}
		}
		#endregion

		#region ScatteringObjectDefinition
		/// <summary>
		/// Object that contains the information of a potential scattered object
		/// </summary>
		[System.Serializable]
		public class ScatteringObjectDefinition
		{
			[Tooltip("The gameobject object that contains the rules\n" +
				"for this type of tree. The lookup with\n" +
				"the terrain system is made using the name of the prefab.")]
			public GameObject prefab;
			[Range(0, 100)]
			[Tooltip("Probability that we will spawn one of this object.\n" +
				"Relative to the other objects.")]
			public float probability = 100.0f;

			[Tooltip("Debug color that represent the object being spawned.\n")]
			public Color color = Color.white;

			public void Init()
			{
				prefab = null;
				probability = 100.0f;
				color = Color.white;
			}
		}
		#endregion

		#region CircularScatteringRule
		[System.Serializable]
		public class CircularScatteringRule
		{
			[Tooltip("The radius in meter of the inner circle where the scattering start.\n")]
			public float innerRadius = 0.5f;

			[Tooltip("The radius in meter of the outer circle where the scattering end.\n")]
			public float outerRadius = 1.0f;

			[Tooltip("The number of object to spawn. X = Min, Y = Max.\n")]
			[CustomVectorLabelsAttribute(null, "Min", "Max")]
			public Vector2Int minMax = new Vector2Int(1, 5);

			[Tooltip("The objects to spawn.\n")]
			public List<ScatteringObjectDefinition> prefabs = new List<ScatteringObjectDefinition>();

			public void Init()
			{
				innerRadius = 0.5f;
				outerRadius = 1.0f;
				minMax = new Vector2Int(1, 5);
				prefabs = new List<ScatteringObjectDefinition>();
			}

			public void DrawSceneUI(Component owner)
			{
#if UNITY_EDITOR
				EditorGUI.BeginChangeCheck();
				Vector2 oldValue = new Vector2(innerRadius, outerRadius);
				DrawEditableCircle(ref innerRadius, Color.red, Color.red, owner.transform);
				DrawEditableCircle(ref outerRadius, Color.yellow, Color.yellow, owner.transform);
				if (EditorGUI.EndChangeCheck())
				{
					oldValue = ClampMinMaxValues(oldValue, new Vector2(innerRadius, outerRadius));
					innerRadius = oldValue.x;
					outerRadius = oldValue.y;
				}

				DrawSimulation(owner.transform);
#endif
			}

			void DrawEditableCircle(ref float value, Color circleColor, Color controlColor, Transform transform)
			{
#if UNITY_EDITOR
				Handles.color = circleColor;
				Handles.DrawWireDisc(
					transform.position,
					transform.up,
					value);

				Handles.color = controlColor;
				value =
					Handles.ScaleValueHandle(value,
						transform.position + transform.forward * value,
						transform.rotation,
						1, Handles.ArrowHandleCap, 1);
#endif
			}

			public static Vector2 ClampMinMaxValues(Vector2 oldValue, Vector2 newValue)
			{
				//Make suer Max is never smaller than min
				//Adjust Min if we need to 
				if (oldValue.y != newValue.y)
				{
					if (newValue.y < 0)
						newValue.y = 0;
					if (newValue.y < newValue.x)
						newValue.x = newValue.y;
				}

				//Make sure Min is never bigger than Max
				//Adjust Max if we need to 
				if (oldValue.x != newValue.x)
				{
					if (newValue.x < 0)
						newValue.x = 0;
					if (newValue.x > newValue.y)
						newValue.y = newValue.x;
				}

				return newValue;
			}

			void DrawSimulation(Transform transform)
			{
#if UNITY_EDITOR
				var numAssets = GetRandomAssetNumber();
				Dictionary<int, int> instancesPerPrefab = new Dictionary<int, int>();

				Handles.color = Color.white;
				for (int i = 0; i < numAssets; i++)
				{
					int index = GetRandomPrefabIndex();
					if (index < 0)
						continue;
					if (!instancesPerPrefab.ContainsKey(index))
						instancesPerPrefab.Add(index, 0);
					instancesPerPrefab[index]++;
				}

				//Validate distance between instances

				//Draw the preview
				List<Vector3> validPos = new List<Vector3>();
				foreach(var prefab in instancesPerPrefab)
				{
					for (int i = 0; i < prefab.Value; i++)
					{
						var position = Vector3.zero;
						bool bValid = false;
						int nbRetry = 0;
						do
						{
							position = transform.position + GetDonutRandomPosition(Vector3.one);
							bValid = ValidatePoint(position, validPos.ToArray(), 10.0f);
							nbRetry++;

							bValid = true; //DESACTIVATED FOR NOW
						}
						while (!bValid && nbRetry <= 10);

						if (bValid)
						{
							Handles.color = new Color(prefabs[prefab.Key].color.r, prefabs[prefab.Key].color.g, prefabs[prefab.Key].color.b, 1.0f);
							Handles.DrawWireCube(position, Vector3.one * 0.1f);
							validPos.Add(position);
						}
						else
						{
							Handles.color = Color.magenta;
							Handles.DrawWireCube(position, Vector3.one * 0.1f);
						}
					}
				}
#endif
			}

			public bool ValidatePoint(Vector3 pos, Vector3[]others, float minDistance)
			{
				for(int i = 0; i < others.Length; i++)
				{
					if (Vector3.Distance(pos, others[i]) < minDistance)
						return false;
				}
				return true;
			}

			public int GetRandomAssetNumber()
			{
				return Random.Range(minMax.x, minMax.y);
			}

			public Vector3 GetDonutRandomPosition(Vector3 scale)
			{
				return GetDonutRandomPosition(innerRadius * scale.x, outerRadius * scale.x);
			}

			public Vector3 GetDonutRandomPosition(float innerRadius, float outerRadius)
			{
				var radiusDiff = outerRadius - innerRadius;
				var randomRatio = Random.Range(0.0f, 1.0f);
				var randomAngle = Random.Range(0.0f, 360.0f);

				return (Quaternion.Euler(0, randomAngle, 0) * Vector3.forward) * (innerRadius + (radiusDiff * randomRatio));
			}

			public int GetRandomPrefabIndex()
			{
				float maxPrefabProbability = prefabs.Count * 100;
				float randomProbability = Random.Range(0, maxPrefabProbability);

				//Convert to index
				float currentProb = 0;
				for (int i = 0; i < prefabs.Count; i++)
				{
					if (currentProb <= randomProbability &&
						randomProbability < currentProb + prefabs[i].probability)
						return i;

					currentProb += prefabs[i].probability;
				}

				return -1;
			}

			#region Initialization of new Rules
			bool isInitialized = false;
			int prefabsSize = 0;
			public void OnEnable()
			{
				prefabsSize = prefabs.Count;
				isInitialized = true;
			}

			public void OnDisable()
			{
				isInitialized = false;
			}

			public void OnValidate()
			{
				if (!isInitialized)
					return;

				if (prefabsSize < prefabs.Count)
				{
					for (int i = prefabsSize; i < prefabs.Count; i++)
						prefabs[i].Init();

					prefabsSize = prefabs.Count;
				}
			}
			#endregion
		}
		#endregion

		public enum RuleType
		{
			Tree = 0,
			Details,
			GameObject
		}

		[Tooltip("The rule type GameObject type is not supported at this time")]
		public RuleType ruleType = RuleType.Tree;
		public bool isDetails { get { return ruleType == RuleType.Details; } }
		public bool isTree { get { return ruleType == RuleType.Tree; } }
		public bool isGameObject { get { return ruleType == RuleType.GameObject; } }

		public bool lockWidthToHeight = false;
		[CustomVectorLabelsAttribute("Height", "Min", "Max")]
		public Vector2 minMaxHeight = Vector3.one;
		[CustomVectorLabelsAttribute("Width", "Min", "Max")]
		public Vector2 minMaxWidth = Vector3.one;

		[Range(0, 100)]
		[SerializeField]
		private float minimumDistanceBetweenInstances = 0;
		public float minDistance { get { return minimumDistanceBetweenInstances * parentScale.x; } }
		private Vector3 parentScale = Vector3.one;

		public List<CircularScatteringRule> rules = new List<CircularScatteringRule>();
		public AlphaSplatDefinition alphamapSplatDefinition;
		public Material alphamapSplatMaterial;

		public Vector2 GetRandomWidthHeight()
		{
			return new Vector2(Random.Range(minMaxWidth.x, minMaxWidth.y), Random.Range(minMaxHeight.x, minMaxHeight.y));
		}

		public ScatteringObjectDef[] GetScatteringDefinitions(Vector3 scale)
		{
			parentScale = scale;
			List<ScatteringObjectDef> defs = new List<ScatteringObjectDef>();

			foreach (var r in rules)
			{
				int numDefs = r.GetRandomAssetNumber();
				for (int i = 0; i < numDefs; i++)
				{
					int index = r.GetRandomPrefabIndex();
					if (index < 0 || r.prefabs[index] == null || r.prefabs[index].prefab == null)
						continue;

					ScatteringObjectDef def = new ScatteringObjectDef(r, index, transform.position, scale);
					defs.Add(def);
				}
			}

			return defs.ToArray();
		}

		bool ValidateDefinitionDistance(ScatteringObjectDef def, List<ScatteringObjectDef> defs, float minDistance)
		{
			if (minDistance <= 0)
				return true;

			for (int i = 0; i < defs.Count; i++)
			{
				if (Vector3.Distance(def.localPosition, defs[i].localPosition) < minDistance)
					return false;
			}
			return true;
		}

		#region Initialization of new Rules
		bool isInitialized = false;
		int rulesSize = 0;
		private void OnEnable()
		{
			rulesSize = rules.Count;
			isInitialized = true;

			for (int i = 0; i < rules.Count; i++)
				rules[i].OnEnable();
		}

		private void OnDisable()
		{
			isInitialized = false;
			for (int i = 0; i < rules.Count; i++)
				rules[i].OnDisable();
		}

		private void OnValidate()
		{
			if (!isInitialized)
				return;

			if (rulesSize < rules.Count)
			{
				for (int i = rulesSize; i < rules.Count; i++)
				{
					rules[i].Init();
					rules[i].OnEnable();
				}

				rulesSize = rules.Count;
			}

			for (int i = 0; i < rules.Count; i++)
				rules[i].OnValidate();
		}
		#endregion
	}
}