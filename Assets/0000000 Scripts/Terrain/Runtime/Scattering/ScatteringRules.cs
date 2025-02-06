using System.Collections.Generic;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	[System.Serializable]
	public class ScatteringRulesContainer
	{
		public ScatteringRule prefab;
		public bool isSelected;
	}

	public class ScatteringRules : MonoBehaviour
	{
		[SerializeField]
		[HideInInspector]
		public List<ScatteringRulesContainer> rules = new List<ScatteringRulesContainer>();
	}
}