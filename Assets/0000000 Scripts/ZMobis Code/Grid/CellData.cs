using UnityEngine;

namespace com.unity.testtrack.partioning.grid
{
	public interface CellData
    {
		public Vector3 position { get; set; }
        public Bounds Bounds { get; set; }
    }
}
