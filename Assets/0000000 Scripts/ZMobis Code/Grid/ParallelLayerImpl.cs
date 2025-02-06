using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace com.unity.testtrack.partioning.grid
{
	public class ParallelLayerImpl : Layer
    {
        //Unit cells
        protected Cell[,]   m_grid;
        protected PartitionGrid      m_owner;
        protected Vector2   m_cellSize;

        public void Create(PartitionGrid owner, int nbCellsX, int nbCellsY)
        {
            m_owner = owner;
            m_grid = new Cell[nbCellsX, nbCellsY];
            m_cellSize = new Vector2(1.0f / nbCellsX, 1.0f / nbCellsY);

            Parallel.For(0, nbCellsX, (x) =>
            {
                Parallel.For(0, nbCellsY, (y) =>
                {
                    Bounds bounds = new Bounds(
                        new Vector3(x * m_cellSize.x + (m_cellSize.x * 0.5f), 0, y * m_cellSize.y + (m_cellSize.y * 0.5f)),
                        new Vector3(m_cellSize.x, float.MaxValue, m_cellSize.y));

                    m_grid[x, y] = new Cell(m_owner, bounds);
                });
            });
        }

        public void Clear()
        {
            Parallel.For(0, m_grid.GetLength(0), (x) =>
            {
                Parallel.For(0, m_grid.GetLength(1), (y) =>
                {
                    m_grid[x, y].Clear();
                });
            });
        }

        public void Insert(IEnumerable<CellData> data)
        {
            Parallel.ForEach(data, cellData =>
            {
                Insert(cellData);
            });
        }

        public void Insert(CellData data)
        {
            var index = GetCellIndex(data.position);
            var cell = GetCell(index.x, index.y);
            cell.AddData(data);
        }

        public void Remove(CellData data)
        {
            var index = GetCellIndex(data.position);
            var cell = GetCell(index.x, index.y);
            cell.RemoveData(data);
        }

        public void Remove(Vector3 position, float radius, Action<CellData> OnCellInstanceRemoved = null, Func<CellData, bool> filterPredicate = null)
        {
            position = m_owner.WorldToGrid(position);
            radius = m_owner.WorldToUnit(radius);

            var cells = FindCells(position, radius, false);
            Parallel.ForEach(cells, (cell) =>
            {
                cell.Remove(position, radius, (t) =>
                {
                    OnCellInstanceRemoved?.Invoke(t);
                },
                filterPredicate);
            });
        }

        public bool HasInstanceWithin(Vector3 position, float radius, Func<CellData, bool> filterPredicate = null)
        {
            position = m_owner.WorldToGrid(position);
            radius = m_owner.WorldToUnit(radius);

            var cells = FindCells(position, radius, false);
            bool foundInstances = false;
            Parallel.ForEach(cells, (cell) =>
            {
                if (cell.HasInstanceWithin(position, radius, filterPredicate))
                    foundInstances = true;
            });

            return foundInstances;
        }

        public IEnumerable<Cell> FindCells(Vector3 position, float radius, bool isWorldPosition = false)
        {
            if (isWorldPosition)
            {
                position = m_owner.WorldToGrid(position);
                radius = m_owner.WorldToUnit(radius);
            }
            Bounds bounds = new Bounds(position, new Vector3(radius * 2, 0.1f, radius * 2));

            return FindCells(bounds, false);
        }

        public IEnumerable<Cell> FindCells(Bounds bounds, bool isWorldPosition = false)
        {
            if (isWorldPosition)
            {
                var center = m_owner.WorldToGrid(bounds.center);
                var size = m_owner.WorldToUnit(bounds.size);
                bounds = new Bounds(center, size);
            }

            var min = bounds.min;
            var max = bounds.max;
            var tx = (int)Mathf.Clamp(Mathf.Floor(min.x / m_cellSize.x), 0, m_grid.GetLength(0) - 1);
            var ty = (int)Mathf.Clamp(Mathf.Floor(min.z / m_cellSize.y), 0, m_grid.GetLength(1) - 1);
            var txMax = (int)Mathf.Clamp(Mathf.Floor(max.x / m_cellSize.x), 0, m_grid.GetLength(0) - 1);
            var tyMax = (int)Mathf.Clamp(Mathf.Floor(max.z / m_cellSize.y), 0, m_grid.GetLength(1) - 1);
            int nbCellsX = (txMax - tx) + 1;
            int nbCellsY = (tyMax - ty) + 1;


            Cell[] cells = new Cell[nbCellsX * nbCellsY];
            Parallel.For(0, nbCellsX, (i) =>
            {
                Parallel.For(0, nbCellsY, (j) =>
                {
                    cells[(i * nbCellsY) + j] = m_grid[(tx + i), (ty + j)];
                });
            });

            return cells;
        }

        public Cell GetCell(int x, int y)
        {
            return m_grid[x, y];
        }

        public Vector2Int GetCellIndex(Vector3 position)
        {
            position = m_owner.WorldToGrid(position);

            var tx = (int)Mathf.Clamp(Mathf.Floor(position.x / m_cellSize.x), 0, m_grid.GetLength(0) - 1);
            var ty = (int)Mathf.Clamp(Mathf.Floor(position.z / m_cellSize.y), 0, m_grid.GetLength(1) - 1);

            return new Vector2Int(tx, ty);
        }

        public int GetDataCount()
        {
            int total = 0;
            object balanceLock = new object();

            Parallel.For(0, m_grid.GetLength(0), (i) =>
            {
                int subTotal = 0;
                for (int j = 0; j < m_grid.GetLength(1); j++)
                    subTotal += m_grid[i, j].Count;

                lock (balanceLock)
                {
                    total += subTotal;
                }
            });

            return total;
        }

        public IEnumerable<CellData> ToArray()
		{
            return null;
		}

        public void OnDrawGizmos()
        {
            for (int x = 0; x < m_grid.GetLength(0); x++)
            {
                for (int y = 0; y < m_grid.GetLength(1); y++)
                    m_grid[x, y].OnDrawGizmos();
            }
        }

        public void OnDrawGizmosSelected()
        {
            for (int x = 0; x < m_grid.GetLength(0); x++)
            {
                for (int y = 0; y < m_grid.GetLength(1); y++)
                    m_grid[x, y].OnDrawGizmosSelected();
            }
        }
    }
}
