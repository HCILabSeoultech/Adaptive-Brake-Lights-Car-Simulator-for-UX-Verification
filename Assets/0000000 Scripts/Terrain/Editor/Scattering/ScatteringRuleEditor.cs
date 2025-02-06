using com.unity.testtrack.utils;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
	#region ScatteringObjectDefinition
	[CustomPropertyDrawer(typeof(ScatteringRule.ScatteringObjectDefinition), true)]
    public class SpawningProbabilityDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Don't make child fields be indented
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var xSize = position.size.x - 20;

            // Calculate rects
            var prefrabRect = new Rect(position.x, position.y, xSize * (150 / 365.0f), position.height);
            var probabilityRect = new Rect(prefrabRect.xMax + 10, position.y, xSize * (150 / 365.0f), position.height);
            var labelRect = new Rect(probabilityRect.xMax + 5, position.y, xSize * (15 / 365.0f), position.height);
            var colorRect = new Rect(labelRect.xMax + 5, position.y, xSize * (50 / 365.0f), position.height);

            EditorGUI.PropertyField(prefrabRect, property.FindPropertyRelative("prefab"), GUIContent.none);
            EditorGUI.PropertyField(probabilityRect, property.FindPropertyRelative("probability"), GUIContent.none);
            EditorGUI.LabelField(labelRect, "%");
            EditorGUI.PropertyField(colorRect, property.FindPropertyRelative("color"), GUIContent.none);

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
    #endregion

    #region CircularScatteringRule
    [CustomPropertyDrawer(typeof(ScatteringRule.CircularScatteringRule), true)]
    public class ScatteringRuleDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var innerRadius = property.FindPropertyRelative("innerRadius");
            var outerRadius = property.FindPropertyRelative("outerRadius");
            var minMax = property.FindPropertyRelative("minMax");
            var prefabs = property.FindPropertyRelative("prefabs");

            EditorGUI.BeginProperty(position, label, property);

            Rect drawRect = position;
            drawRect.height = EditorGUIUtility.singleLineHeight;

            EditorGUI.PropertyField(drawRect, innerRadius);

            drawRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(drawRect, outerRadius);

            drawRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(drawRect, minMax);

            drawRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(drawRect, prefabs, true);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var innerRadius = property.FindPropertyRelative("innerRadius");
            var outerRadius = property.FindPropertyRelative("outerRadius");
            var minMax = property.FindPropertyRelative("minMax");
            var prefabs = property.FindPropertyRelative("prefabs");

            return (3 * EditorGUIUtility.singleLineHeight + 3 * EditorGUIUtility.standardVerticalSpacing) + EditorGUI.GetPropertyHeight(prefabs);
        }
    }
    #endregion

    #region ScatteringRule
    [CustomEditor(typeof(ScatteringRule))]
    [CanEditMultipleObjects]
    public class ScatteringRuleEditor : Editor
    {
        public List<bool> editSelection = new List<bool>();
        
        static class SelectionBox
        {
            public static GUIStyle style = new GUIStyle();

            public static int textSize = 20;
            public static int topSpace = 10;
            public static int bottomSpace = 10;

            public static int GetHeight(int nbItems)
            {
                return (nbItems * SelectionBox.textSize) + padding;
            }

            public static int padding { get { return SelectionBox.topSpace + SelectionBox.bottomSpace; } }
        }

        private void OnEnable()
        {
            UdpateEditSelectionArray((ScatteringRule)target);

            SelectionBox.style.normal.background = new Texture2D(1, 1);
            SelectionBox.style.normal.background.SetPixel(0, 0, Color.black * 0.50f);
            SelectionBox.style.normal.background.Apply();
		}

        protected virtual void OnSceneGUI()
        {
            ScatteringRule component = (ScatteringRule)target;

            if (component == null)
                return;

            DrawSelectionBox(component);

            var defaultStyle = GetDefaultInSceneStyle();

            if (editSelection[0])
            {
                DrawObjectInspector(component, defaultStyle);
                DrawObjectPreview(component);
            }
            DrawRules(component);
        }

        public override void OnInspectorGUI()
        {
            ScatteringRule component = (ScatteringRule)target;

            if (component == null)
                return;

            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(component), typeof(MonoScript), false);
            EditorGUI.EndDisabledGroup();

            var ruleTypeProp = serializedObject.FindProperty("ruleType");
            var lockWidthToHeightProp = serializedObject.FindProperty("lockWidthToHeight");
            var heightProp = serializedObject.FindProperty("minMaxHeight");
            var widthProp = serializedObject.FindProperty("minMaxWidth");

            EditorGUILayout.PropertyField(ruleTypeProp);
            EditorGUILayout.PropertyField(lockWidthToHeightProp);

			GUI.SetNextControlName("Height");
			float[] s_Vector2Floats = new float[2];
			s_Vector2Floats[0] = heightProp.vector2Value.x;
			s_Vector2Floats[1] = heightProp.vector2Value.y;

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(heightProp, new GUIContent("Height"), true);
			if (EditorGUI.EndChangeCheck())
			{
				heightProp.vector2Value = ScatteringRule.CircularScatteringRule.ClampMinMaxValues(new Vector2(s_Vector2Floats[0], s_Vector2Floats[1]), heightProp.vector2Value);
			}

			EditorGUI.BeginDisabledGroup(lockWidthToHeightProp.boolValue);
			EditorGUILayout.PropertyField(widthProp, new GUIContent("Width"), true);
            if (lockWidthToHeightProp.boolValue)
			    widthProp.vector2Value = heightProp.vector2Value;
			EditorGUI.EndDisabledGroup();

            //DrawMinMaxGui("Height", ref component.minMaxHeight, ref heightProp);
            //         DrawMinMaxGui("Width", ref component.minMaxWidth, ref widthProp);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumDistanceBetweenInstances"), true);
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty("alphamapSplatDefinition"), new GUIContent("Splat definition"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("alphamapSplatMaterial"), new GUIContent("Splat Material"), true);

            //Draw our Rules array
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rules"), new GUIContent("Rules"), true);

   //         if (GUI.GetNameOfFocusedControl() == "Height")
   //{
   //             Debug.Log("Height");
   //}

            // Apply changes to the serializedProperty - always do this at the end of OnInspectorGUI.
            serializedObject.ApplyModifiedProperties();
        }

        bool DrawMinMaxGui(string label, ref Vector2 value, ref SerializedProperty prop)
        {
            float[] s_Vector2Floats = new float[2];

            //Height min max
            EditorGUI.BeginChangeCheck();
            s_Vector2Floats[0] = value.x;
            s_Vector2Floats[1] = value.y;
            Rect r = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, new GUIContent(label)), EditorStyles.numberField, null);
            EditorGUI.MultiFloatField(r, new GUIContent(label), new GUIContent[] { new GUIContent("min"), new GUIContent("max") }, s_Vector2Floats);
            if (EditorGUI.EndChangeCheck())
            {
                value = ScatteringRule.CircularScatteringRule.ClampMinMaxValues(value, new Vector2(s_Vector2Floats[0], s_Vector2Floats[1]));
                //Set the value on the property
                prop.vector2Value = value;

                return true;
            }

            return false;
        }

        GUIStyle GetDefaultInSceneStyle()
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.green;

            return style;
        }

        #region In Scene Selection Box
        void DrawSelectionBox(ScatteringRule component)
        {
            Handles.BeginGUI();

            var areaHeight = SelectionBox.GetHeight(editSelection.Count);


            // Starts an area to draw elements
            GUILayout.BeginArea(new Rect(10, 10, 200, areaHeight), SelectionBox.style);
            GUILayout.Box(SelectionBox.style.normal.background, GUILayout.Width(200), GUILayout.Height(areaHeight));
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(20, 20, 190, areaHeight - SelectionBox.padding));
            editSelection[0] = GUILayout.Toggle(editSelection[0], "Edit object scale", GUILayout.Width(200), GUILayout.Height(20));

            for (int i = 1; i < editSelection.Count; i++)
                editSelection[i] = GUILayout.Toggle(editSelection[i], "Edit Rule " + i, GUILayout.Width(200), GUILayout.Height(20));
            GUILayout.EndArea();

            Handles.EndGUI();

            //Update the UI if needed
            if (editSelection.Count != component.rules.Count + 1)
                UdpateEditSelectionArray(component);
        }

        void UdpateEditSelectionArray(ScatteringRule component)
        {
            editSelection.Clear();
            editSelection.Add(true); //0 = main object, all the other are rules

            if (component != null)
            {
                foreach (var r in component.rules)
                    editSelection.Add(false);
            }
        }
        #endregion

        #region Object Scale
        void DrawObjectPreview(ScatteringRule component)
        {
            if (!component.gameObject.ComputeBounds(out var bounds, true))
                return;
            
            Vector2 oldHeightMinMax = component.minMaxHeight;
            Vector2 oldWidthMinMax = component.minMaxWidth;

            DrawScalingCube(bounds.center, bounds.size, ref component.minMaxHeight.x, ref component.minMaxWidth.x, component.transform, Color.red, Color.red);
            DrawScalingCube(bounds.center, bounds.size, ref component.minMaxHeight.y, ref component.minMaxWidth.y, component.transform, Color.yellow, Color.yellow);
            DrawMinDistanceRadius(bounds.center - (Vector3.up * (bounds.size.y/2)), component.minDistance);

            component.minMaxHeight = ScatteringRule.CircularScatteringRule.ClampMinMaxValues(oldHeightMinMax, component.minMaxHeight);
            component.minMaxWidth = ScatteringRule.CircularScatteringRule.ClampMinMaxValues(oldWidthMinMax, component.minMaxWidth);
        }

        void DrawMinDistanceRadius(Vector3 position, float radius)
        {
            Handles.color = Color.white;
            Handles.DrawWireDisc(position, Vector3.up, radius);
        }

        void DrawScalingCube(Vector3 center, Vector3 size, ref float heightScaleValue, ref float widthScaleValue, Transform transform, Color cubeColor, Color arrowsColor)
        {
            var scalledSize = new Vector3(size.x * widthScaleValue, size.y * heightScaleValue, size.z * widthScaleValue);
            var heightCenterOffsetRatio = 1.0f - (scalledSize.y / size.y);
            var widthCenterOffsetRatio = 1.0f - (scalledSize.x / size.x);
            var offsetedCenter = center + (-transform.up * ((heightCenterOffsetRatio * size.y) / 2));

            Handles.color = cubeColor;
            Handles.DrawWireCube(offsetedCenter, scalledSize);

			Handles.color = arrowsColor;
			//Draw top arrow
			heightScaleValue = Handles.ScaleValueHandle(heightScaleValue,
						  offsetedCenter + (transform.up * (scalledSize.y / 2)),
					Quaternion.Euler(transform.rotation.eulerAngles + new Vector3(-90, 0, 0)),
					1, Handles.ArrowHandleCap, 1);

			//Draw side arrow
			widthScaleValue = Handles.ScaleValueHandle(widthScaleValue,
						  offsetedCenter + (transform.forward * (scalledSize.z / 2)),
					Quaternion.Euler(transform.rotation.eulerAngles),
					1, Handles.ArrowHandleCap, 1);

			//Draw forward arrow
			widthScaleValue = Handles.ScaleValueHandle(widthScaleValue,
						  offsetedCenter + (transform.right * (scalledSize.x / 2)),
					Quaternion.Euler(transform.rotation.eulerAngles + new Vector3(0, 90, 0)),
					1, Handles.ArrowHandleCap, 1);
		}

        Vector3 DrawObjectInspector(ScatteringRule component, GUIStyle style)
        {
            Vector3 position = component.transform.position + Vector3.up * 2f;
            string posString = position.ToString();
            posString += "\nMin Max Height: " + component.minMaxHeight.ToString();
            posString += "\nMin Max Width: " + component.minMaxWidth.ToString();
            Handles.Label(position, posString, style);

            return position;
        }
        #endregion

        #region Rules
        void DrawRules(ScatteringRule component)
        {
            for (int i = 0; i < component.rules.Count; i++)
            {
                if (editSelection[i + 1])
                    component.rules[i].DrawSceneUI(component);
            }
        }
        #endregion
    }
	#endregion
}
