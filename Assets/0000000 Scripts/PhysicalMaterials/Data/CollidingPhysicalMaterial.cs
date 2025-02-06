#if UNITY_EDITOR
using UnityEditor;
#endif
using com.unity.testtrack.physics;
using VolvoCars.Data;

namespace com.unity.testtrack.Data
{
	/// <summary></summary>
	[System.Serializable]
    public struct WheelPhysicalProperty
    {
        public enum Wheel
        {
            FL, /// <summary>Front left wheel</summary>
            FR, /// <summary>Front right wheel</summary>
            RL, /// <summary>Rear left wheel</summary>
            RR, /// <summary>Rear right wheel</summary>
        }

        public PhysicalProperty fL;
        public PhysicalProperty fR;
        public PhysicalProperty rL;
        public PhysicalProperty rR;
    }

    public class CollidingPhysicalMaterial : Data<WheelPhysicalProperty> { }

#if UNITY_EDITOR
    [CustomEditor(typeof(CollidingPhysicalMaterial))]
    public class CollidingPhysicalMaterialEditor : DataEditor<WheelPhysicalProperty>
    {

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("The physical material that we are currently colliding", MessageType.None, true);
            base.OnInspectorGUI();
        }

    }
#endif

}
