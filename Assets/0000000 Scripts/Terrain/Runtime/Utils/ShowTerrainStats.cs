using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShowTerrainStats : MonoBehaviour
{
    public int fontSize = 15;
    public bool verbose = false;

    List<Terrain> m_terrains = new List<Terrain>();

    private float updateInterval;
    private string gui = "";
    private double lastInterval; // Last interval end time

    private Dictionary<GameObject, int> m_countBytype = new Dictionary<GameObject, int>();

    // Start is called before the first frame update
    void Start()
    {
        m_terrains = FindObjectsOfType<Terrain>(true).ToList();
        this.updateInterval = 1f;

        //Create all the types
        foreach (var t in m_terrains)
        {
            foreach (var proto in t.terrainData.treePrototypes)
            {
                if (!m_countBytype.ContainsKey(proto.prefab))
                    m_countBytype.Add(proto.prefab, 0);
            }
        }

        CalculateStatsByType();
    }

    // Update is called once per frame
    void Update()
    {
        //++this.frames;
        float timeNow = Time.realtimeSinceStartup;
        if (timeNow > (this.lastInterval + this.updateInterval))
        {
            int nbTreeInstances = 0;
            foreach (var t in m_terrains.Where(t => t.gameObject.activeSelf && t.drawTreesAndFoliage))
                nbTreeInstances += t.terrainData.treeInstanceCount;

            var typeCounts = "";
            if (verbose)
            {
                foreach (var k in m_countBytype)
                    typeCounts += "\n  " + k.Key.name + " : " + k.Value;
            }

            this.gui = "Terrain Stats:" +
                "\n Total Tree Instances: " + nbTreeInstances + typeCounts;

            this.lastInterval = timeNow;
        }
    }

    void CalculateStatsByType()
    {
        //Reset
        var keys = m_countBytype.Keys.ToList();
        foreach (var k in keys)
            m_countBytype[k] = 0;

        //Recalculate
        foreach (var t in m_terrains)
        {
            foreach (var instance in t.terrainData.treeInstances)
            {
                var proto = t.terrainData.treePrototypes[instance.prototypeIndex];
                if (m_countBytype.ContainsKey(proto.prefab))
                    m_countBytype[proto.prefab]++;
            }
        }
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = fontSize;
        style.normal.textColor = Color.white;

        var lines = this.gui.Split("\n");
        var lenghtBiggestLine = 0;
        foreach (var line in lines)
            if (lenghtBiggestLine < line.Length)
                lenghtBiggestLine = line.Length;

        float w = (lenghtBiggestLine * (style.fontSize * 0.55f)), h = (lines.Length + 1) * (style.fontSize * 1.15f);
        GUILayout.BeginArea(new Rect(10, 0, w, h), "", GUI.skin.box);
        GUILayout.Label(this.gui, style);
        GUILayout.EndArea();
    }
}