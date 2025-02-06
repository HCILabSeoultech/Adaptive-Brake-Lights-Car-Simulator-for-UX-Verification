using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using com.unity.testtrack.utils;

namespace com.unity.testtrack.terrainsystem.editor
{
	public class RemoveTilesWizard : ScriptableWizard
	{
		public List<GameObject> m_exclusionBoxes = new List<GameObject>();
		public Transform[] m_selectedObjects;

		protected override bool DrawWizardGUI()
		{
			return base.DrawWizardGUI();
		}

		internal virtual void OnWizardUpdate()
		{
		}

		private void CreateGUI()
		{
			m_selectedObjects = Selection.GetFiltered<Transform>(SelectionMode.TopLevel);
			MiscTools.removeTileWizard = null;
		}

		internal void OnWizardCreate()
		{
			if (m_exclusionBoxes.Count == 0)
				return;

			for (int i = m_selectedObjects.Length - 1; i >= 0; i--)
			{
				if (!m_selectedObjects[i].gameObject.ComputeBounds(out var objBounds, true))
					continue;

				objBounds = new Bounds(objBounds.center, new Vector3(objBounds.size.x, float.MaxValue, objBounds.size.z));
				bool deleteObject = true;
				foreach (var excBox in m_exclusionBoxes)
				{
					if (!excBox.ComputeBounds(out var bounds, true))
						continue;

					if (objBounds.Intersects(bounds))
					{
						deleteObject = false;
						break;
					}
				}

				if (deleteObject)
				{
					try
					{
						Undo.RecordObject(m_selectedObjects[i].gameObject, "RemovingTiles");
						DestroyImmediate(m_selectedObjects[i].gameObject);
					}
					finally { }
				}
			}
		}
	}
}
