using com.unity.testtrack.partioning.grid;
using System.Collections.Generic;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	public abstract class IExclusionZone : MonoBehaviour
	{
		abstract public void ComputeBounds();
		abstract public bool InZone(Vector3 position, string objectName = null);
		abstract public IEnumerable<Cell> GetIntersectingCells(PartitionGrid grid);
		abstract public Vector3 center { get; }
		abstract public Vector3 size { get; }
		abstract public float radius { get; }
		abstract public List<ScatteringRule> filters { get; }
	}
}