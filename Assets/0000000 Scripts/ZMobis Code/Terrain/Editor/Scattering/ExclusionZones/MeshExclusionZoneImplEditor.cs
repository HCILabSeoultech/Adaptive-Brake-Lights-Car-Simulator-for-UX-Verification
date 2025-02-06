using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.unity.testtrack.terrainsystem
{
    [CustomEditor(typeof(MeshExclusionZoneImpl))]
    public class MeshExclusionZoneImplEditor : Editor
    {
        public void OnEnable()
        {
            var exclusionZone = target as MeshExclusionZoneImpl;
            if (exclusionZone.m_material == null)
                exclusionZone.m_material = new Material(Shader.Find("Shader Graphs/DefaultExclusionZone"));
        }
    }
}