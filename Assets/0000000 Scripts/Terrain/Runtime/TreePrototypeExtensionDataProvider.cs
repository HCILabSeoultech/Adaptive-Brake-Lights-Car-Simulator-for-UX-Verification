using com.unity.testtrack.terrainsystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TreePrototypeExtensionDataProvider : MonoBehaviour
{
    [System.Serializable]
    public class ExtraData
    {
        public GameObject m_key;
		public AlphaSplatDefinition alphamapSplatDefinition;
		public Material alphamapSplatMaterial;

		public ExtraData(GameObject key)
        {
            m_key = key;
        }
    }

    [SerializeField]
    [HideInInspector]
    private ExtraData[] m_extraData;
    Dictionary<GameObject, ExtraData> m_classifiedExtraData = new Dictionary<GameObject, ExtraData>();

	public void Initialize(TreePrototype[] protos, Action<ExtraData> OnInsert = null)
	{
		//Classified our saved data for fast lookup
		var tempClassifedExraData = new Dictionary<GameObject, ExtraData>();
		if (m_extraData != null)
		{
			foreach (var ed in m_extraData)
			{
				tempClassifedExraData.Add(ed.m_key, ed);
				OnInsert?.Invoke(ed);
			}
		}

		m_classifiedExtraData.Clear();
		if (protos != null)
		{
			foreach (var proto in protos)
			{
				if (tempClassifedExraData.ContainsKey(proto.prefab))
					m_classifiedExtraData.Add(proto.prefab, tempClassifedExraData[proto.prefab]);
				else
					m_classifiedExtraData.Add(proto.prefab, new ExtraData(proto.prefab));

				OnInsert?.Invoke(m_classifiedExtraData[proto.prefab]);
			}
		}

		SyncExtraDataArray();
	}

	public void Remove(TreePrototype[] protos)
	{
		if (protos == null)
			return;

		foreach (var proto in protos.Where(proto => m_classifiedExtraData.ContainsKey(proto.prefab)))
			m_classifiedExtraData.Remove(proto.prefab);

		SyncExtraDataArray();
	}

	public void Remove(TreePrototype proto)
	{
		if (m_classifiedExtraData.ContainsKey(proto.prefab))
			m_classifiedExtraData.Remove(proto.prefab);
		else
			Debug.LogWarning("Could not find key for tree proto( " + proto.prefab.ToString() + ")");

		//Let the user call this, since it pretty slow
		SyncExtraDataArray();
	}

	public void Add(TreePrototype[] protos)
	{
		if (protos == null)
			return;

		foreach (var proto in protos.Where(proto => !m_classifiedExtraData.ContainsKey(proto.prefab)))
			m_classifiedExtraData.Add(proto.prefab, new ExtraData(proto.prefab));

		SyncExtraDataArray();
	}

	public void Add(TreePrototype proto)
	{
		if (proto == null)
			return;

		if (m_classifiedExtraData.ContainsKey(proto.prefab))
			m_classifiedExtraData.Add(proto.prefab, new ExtraData(proto.prefab));
		else
			Debug.LogWarning("Could not find key for tree( " + proto.prefab.ToString() + ")");
	}

	public void Add(TreePrototype proto, ExtraData extraData)
	{
		if (!m_classifiedExtraData.ContainsKey(proto.prefab))
			m_classifiedExtraData.Add(proto.prefab, extraData == null ? new ExtraData(proto.prefab) : extraData);
		else
			Debug.LogWarning("Duplicate key for tree( " + proto.prefab.ToString() + ")");

		//Let the user call this, since it pretty slow
		SyncExtraDataArray();
	}

	public ExtraData Get(TreePrototype proto)
	{
		if (m_classifiedExtraData.TryGetValue(proto.prefab, out var result))
			return result;

		return null;
	}

	public void SyncExtraDataArray()
	{
		m_extraData = m_classifiedExtraData.Values.ToArray();
	}
}
