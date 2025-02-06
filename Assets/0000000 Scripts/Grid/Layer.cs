using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.unity.testtrack.partioning.grid
{
	public interface Layer
    {
        public void Create(PartitionGrid owner, int nbCellsX, int nbCellsY);

        public void Clear();

        public void Insert(IEnumerable<CellData> data);

        public void Insert(CellData data);

        public void Remove(CellData data);

        public void Remove(Vector3 position, float radius, Action<CellData> OnCellInstanceRemoved = null, Func<CellData, bool> filterPredicate = null);

        public bool HasInstanceWithin(Vector3 position, float radius, Func<CellData, bool> filterPredicate = null);

        public IEnumerable<Cell> FindCells(Vector3 position, float radius, bool isWorldPosition = false);

        public IEnumerable<Cell> FindCells(Bounds bounds, bool isWorldPosition = false);

        public Cell GetCell(int x, int y);

        public Vector2Int GetCellIndex(Vector3 position);

        public int GetDataCount();

        public IEnumerable<CellData> ToArray();

        public void OnDrawGizmos();

        public void OnDrawGizmosSelected();
    }
}
