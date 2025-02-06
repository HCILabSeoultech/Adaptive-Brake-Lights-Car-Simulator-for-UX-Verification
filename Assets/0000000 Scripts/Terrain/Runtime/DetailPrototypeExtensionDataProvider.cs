using com.unity.testtrack.terrainsystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DetailPrototypeExtensionDataProvider : MonoBehaviour
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

	public void Initialize(DetailPrototype[] protos, Action<ExtraData> OnInsert = null)
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
				if (tempClassifedExraData.ContainsKey(proto.prototype))
					m_classifiedExtraData.Add(proto.prototype, tempClassifedExraData[proto.prototype]);
				else
					m_classifiedExtraData.Add(proto.prototype, new ExtraData(proto.prototype));

				OnInsert?.Invoke(m_classifiedExtraData[proto.prototype]);
			}
		}

		SyncExtraDataArray();
	}

	public void Remove(DetailPrototype[] protos)
	{
		if (protos == null)
			return;

		foreach (var proto in protos.Where(proto => m_classifiedExtraData.ContainsKey(proto.prototype)))
			m_classifiedExtraData.Remove(proto.prototype);

		SyncExtraDataArray();
	}

	public void Remove(DetailPrototype proto)
	{
		if (m_classifiedExtraData.ContainsKey(proto.prototype))
			m_classifiedExtraData.Remove(proto.prototype);
		else
			Debug.LogWarning("Could not find key for detail proto( " + proto.prototype.ToString() + ")");

		//Let the user call this, since it pretty slow
		SyncExtraDataArray();
	}

	public void Add(DetailPrototype[] protos)
	{
		if (protos == null)
			return;

		foreach (var proto in protos.Where(proto => !m_classifiedExtraData.ContainsKey(proto.prototype)))
			m_classifiedExtraData.Add(proto.prototype, new ExtraData(proto.prototype));

		SyncExtraDataArray();
	}

	public void Add(DetailPrototype proto)
	{
		if (proto == null)
			return;

		if (m_classifiedExtraData.ContainsKey(proto.prototype))
			m_classifiedExtraData.Add(proto.prototype, new ExtraData(proto.prototype));
		else
			Debug.LogWarning("Could not find key for detail( " + proto.prototype.ToString() + ")");
	}

	public void Add(DetailPrototype proto, ExtraData extraData)
	{
		if (!m_classifiedExtraData.ContainsKey(proto.prototype))
			m_classifiedExtraData.Add(proto.prototype, extraData == null ? new ExtraData(proto.prototype) : extraData);
		else
			Debug.LogWarning("Duplicate key for detail( " + proto.prototype.ToString() + ")");

		//Let the user call this, since it pretty slow
		SyncExtraDataArray();
	}

	public ExtraData Get(DetailPrototype proto)
	{
		if (m_classifiedExtraData.TryGetValue(proto.prototype, out var result))
			return result;

		return null;
	}

	public void SyncExtraDataArray()
	{
		m_extraData = m_classifiedExtraData.Values.ToArray();
	}
}
