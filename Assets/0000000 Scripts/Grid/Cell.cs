using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.unity.testtrack.partioning.grid
{
	public class Cell
	{
		protected object			m_listLock = new object();
		protected List<CellData>	m_data = new List<CellData>();
		protected Bounds			m_bounds;
		protected PartitionGrid				m_owner;

		public virtual List<CellData>	data { get { return m_data; } }
		public virtual Bounds			bounds { get { return m_bounds; } }
		public virtual bool				hasData { get { return m_data.Count > 0; } }
		public virtual int				Count { get { return m_data.Count; } }

		public Cell(PartitionGrid owner, Bounds bounds)
		{
			m_bounds = bounds;
			m_owner = owner;
		}

		public virtual void Clear()
		{
			lock (m_listLock)
			{
				m_data.Clear();
			}
		}

		public virtual void AddData(CellData[] datas)
		{
			lock (m_listLock)
			{
				m_data.AddRange(datas);
			}
		}

		public virtual void AddData(CellData data)
		{
			lock (m_listLock)
			{
				m_data.Add(data);
			}
		}

		public virtual void AddDataAt(int index, CellData data)
		{
			lock (m_listLock)
			{
				m_data.Insert(index, data);
			}
		}

		//Slow
		public virtual void RemoveData(CellData data)
		{
			lock (m_listLock)
			{
				m_data.Remove(data);
			}
		}

		//fast
		public virtual void Remove(int index)
		{
			lock (m_listLock)
			{
				m_data.RemoveAt(index);
			}
		}

		public virtual void RemoveRange(int startIndex, int count)
		{
			lock (m_listLock)
			{
				m_data.RemoveRange(startIndex, count);
			}
		}

		public virtual void Remove(Vector3 pos, float radius, Action<CellData> OnRemovedData = null, Func<CellData, bool> filterPredicate = null)
		{
			float sqrtRadius = radius * radius;
			for (int i = data.Count - 1; i >= 0; i--)
			{
				if (filterPredicate?.Invoke(data[i]) == false)
					continue;

				var unitPos = m_owner.WorldToGrid(data[i].position);
				if (IsInRadius(unitPos, pos, sqrtRadius))
				{
					OnRemovedData?.Invoke(data[i]);
					Remove(i);
				}
			}
		}

		public bool HasInstanceWithin(Vector3 pos, float radius, Func<CellData, bool> filterPredicate = null)
		{
			float sqrtRadius = radius * radius;
			for (int i = data.Count - 1; i >= 0; i--)
			{
				if (filterPredicate?.Invoke(data[i]) == false)
					continue;

				var unitPos = m_owner.WorldToGrid(data[i].position);
				if (IsInRadius(unitPos, pos, sqrtRadius))
					return true;
			}

			return false;
		}

		private bool IsInRadius(Vector3 srcPosition, Vector3 regionCenter, float regionSqrRadius)
		{
			Vector2 offset = new Vector2(srcPosition.x - regionCenter.x, srcPosition.z - regionCenter.z);
			if (Vector2.SqrMagnitude(offset) <= regionSqrRadius)
				return true;

			return false;
		}

		internal virtual void OnDrawGizmos()
		{
			OnDrawGizmosSelected();
		}

		internal virtual void OnDrawGizmosSelected()
		{
			Gizmos.color = data.Count > 0 ? Color.red : Color.green;
			Vector3 wpos = m_owner.UnitToWorld(bounds.center);
			Vector3 wsize = m_owner.UnitToGrid(bounds.size);
			wsize.y = m_owner.size.y;

			Gizmos.DrawWireCube(wpos, wsize);
		}
	}
}
