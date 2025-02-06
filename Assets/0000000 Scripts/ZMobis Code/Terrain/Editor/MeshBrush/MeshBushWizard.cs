using UnityEditor;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	public class MeshBushWizard : ScriptableWizard
	{
		public bool			m_FromTop			= true;
		[Range(-2, 2)]
		public float		m_Strength			= 1;
		public float		m_HeightOffset		= 0.001f;
		public Transform[]	m_SelectedObjects;
		public bool			m_IncludeChildren	= false;

		[Header("Terrain deformer")]
		public float		m_SmoothRadius		= 5;

		protected override bool DrawWizardGUI()
		{
			return base.DrawWizardGUI();
		}

		internal virtual void OnWizardUpdate()
		{
		}

		private void CreateGUI()
		{
			m_SelectedObjects = Selection.GetFiltered<Transform>(SelectionMode.TopLevel);
		}

		internal void OnWizardCreate()
		{
			MeshBrushImpl.PaintSelectionOptions settings = new MeshBrushImpl.PaintSelectionOptions(m_Strength, this.m_FromTop ? -1 : 1, m_SelectedObjects, m_IncludeChildren, m_SmoothRadius, m_HeightOffset, null);
			MeshBrushImpl.PaintSelection(settings);
		}
	}
}
