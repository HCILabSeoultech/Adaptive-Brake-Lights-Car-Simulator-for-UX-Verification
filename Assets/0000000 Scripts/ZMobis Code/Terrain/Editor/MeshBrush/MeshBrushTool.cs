using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	public class MeshBrushTool : Editor
	{
		[Shortcut("GameObject/Terrain/Paint Selection Top", null, KeyCode.U, ShortcutModifiers.Shift | ShortcutModifiers.Alt)]
		private static void PaintSelectionTop()
		{
			if (!CanPaintSelection())
				return;

			var settings = new MeshBrushImpl.PaintSelectionOptions(1, -1, Selection.GetFiltered<Transform>(SelectionMode.TopLevel), false);
			MeshBrushImpl.PaintSelection(settings);
		}

		[Shortcut("GameObject/Terrain/Paint Selection Bottom", null, KeyCode.D, ShortcutModifiers.Shift | ShortcutModifiers.Alt)]
		private static void PaintSelectionBottom()
		{
			if (!CanPaintSelection())
				return;

			var settings = new MeshBrushImpl.PaintSelectionOptions(1, 1, Selection.GetFiltered<Transform>(SelectionMode.TopLevel), false);
			MeshBrushImpl.PaintSelection(settings);
		}

		[MenuItem("GameObject/Terrain/Paint Selection #&p", false, 0)]
		private static void PaintSelection()
		{
			ScriptableWizard.DisplayWizard<MeshBushWizard>("Paint Selection Settings", "Apply");
		}

		[MenuItem("GameObject/Terrain/Paint Selection #&p", true, 0)]
		private static bool CanPaintSelection()
		{
			return MeshBrushImpl.GetMeshRenderersFromSelection(new MeshBrushImpl.PaintSelectionOptions(1, 1, Selection.GetFiltered<Transform>(SelectionMode.TopLevel), false)).Length > 0;
		}
	}
}
