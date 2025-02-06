using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.unity.testtrack.partioning.grid
{
	public class PartitionGrid
    {
        private Layer m_rootLayer = new ParallelLayerImpl();
		public Vector3 position { get; set; }
        public Vector3 size { get; set; }

        public PartitionGrid(Vector3 position, Vector3 size, int nbCellsX, int nbCellsY, bool isParallel = true)
		{
            this.position = position;
            this.size = size;

            if (!isParallel)
                m_rootLayer = new LayerImpl();

            m_rootLayer.Create(this, nbCellsX, nbCellsY);
		}

        public void Clear()
        {
            m_rootLayer.Clear();
        }

        public void Insert(IEnumerable<CellData> data)
        {
            m_rootLayer.Insert(data);
        }

        public void Insert(CellData data)
        {
            m_rootLayer.Insert(data);
        }

        public void Remove(CellData data)
        {
            m_rootLayer.Remove(data);
        }

		public void Remove(Vector3 position, float radius, Action<CellData> OnCellInstanceRemoved = null, Func<CellData, bool> filterPredicate = null)
		{
            m_rootLayer.Remove(position, radius, OnCellInstanceRemoved, filterPredicate);
		}

        public bool HasInstanceWithin(Vector3 position, float radius, Func<CellData, bool> filterPredicate = null)
        {
            return m_rootLayer.HasInstanceWithin(position, radius, filterPredicate);
        }

        public IEnumerable<Cell> FindCells(Vector3 position, float radius)
		{
            return m_rootLayer.FindCells(position, radius, true);
        }

        public IEnumerable<Cell> FindCells(Bounds bounds)
		{
            return m_rootLayer.FindCells(bounds, true);
        }

        public List<T> GetCellData<T>() where T : CellData
		{
            throw new NotImplementedException();
		}

        public int GetDataCount()
		{
            return m_rootLayer.GetDataCount();
		}

        public CellData[] ToArray()
        {
            return m_rootLayer.ToArray().ToArray();
        }

        public virtual void OnDrawGizmos()
        {
            m_rootLayer.OnDrawGizmos();
        }

        public virtual void OnDrawGizmosSelected()
        {
            m_rootLayer.OnDrawGizmosSelected();
        }

        public Vector3 UnitToGrid(Vector3 pos)
        {
            pos.Set(pos.x * size.x, pos.y * size.y, pos.z * size.z);
            return pos;
        }

        public Vector3 UnitToWorld(Vector3 pos)
        {
            return UnitToGrid(pos) + position;
        }

        public Vector3 WorldToUnit(Vector3 pos)
        {
            pos.Set(pos.x / size.x, pos.y / size.y, pos.z / size.z);
            return pos;
        }

        public float WorldToUnit(float val)
        {
            return val / size.x;
        }

        public Vector3 WorldToGrid(Vector3 pos)
        {
            pos -= position;
            pos.Set(pos.x / size.x, pos.y / size.y, pos.z / size.z);
            return pos;
        }
    }
}
