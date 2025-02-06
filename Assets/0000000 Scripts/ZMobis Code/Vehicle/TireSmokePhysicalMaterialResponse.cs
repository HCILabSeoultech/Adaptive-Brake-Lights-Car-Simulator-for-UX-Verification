using com.unity.testtrack.Data;
using com.unity.testtrack.physics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;

namespace com.unity.testtrack.vehicle
{
	public class TireSmokePhysicalMaterialResponse : MonoBehaviour
    {
        [SerializeField] WheelPhysicalProperty.Wheel    m_wheel;
        [SerializeField] WheelCollider                  m_wheelCollider;
        [SerializeField] CollidingPhysicalMaterial      m_collidingMaterial;
        [SerializeField] Transform                      m_parent;
        [Range(0, 1)]
        [SerializeField] float                          m_minForwardSlipValue = 0.05f;
        [Range(0, 1)]
        [SerializeField] float                          m_minSidewaySlipValue = 0.05f;
        [SerializeField] int                            m_preloadedEmitterCount = 7;

        private PhysicalProperty    m_currentProperty = null;
        private Stack<VisualEffect> m_emiterPool = new Stack<VisualEffect>();
        private Dictionary<PhysicalProperty, VisualEffect> m_emiters = new Dictionary<PhysicalProperty, VisualEffect>();

        private void OnEnable()
        {
            if (m_wheelCollider == null)
                m_wheelCollider = GetComponent<WheelCollider>();

            m_currentProperty = null;
            m_collidingMaterial?.Subscribe(OnPhysicalMaterialChange);

            for (int i = 0; i < m_preloadedEmitterCount; i++)
                m_emiterPool.Push(CreateEmitter(m_parent));
        }

        private void OnDisable()
        {
            m_collidingMaterial?.Unsubscribe(OnPhysicalMaterialChange);

            foreach (var emitter in m_emiterPool.Where(emitter => emitter != null))
                DestroyImmediate(emitter);
            m_emiterPool.Clear();

            m_currentProperty = null;
        }

        private void FixedUpdate()
        {
            ResetSlipAmount();
            if (m_currentProperty == null)
                return;

            float amount = 0;
            if (m_wheelCollider.GetGroundHit(out var hit))
            {
                var forwardSlip = Mathf.Abs(hit.forwardSlip);
                if (forwardSlip < m_minForwardSlipValue)
                    forwardSlip = 0;
                else
                {
                    Remap_float(forwardSlip, new Vector2(m_minForwardSlipValue, 1), new Vector2(0, 1), out var newSlip);
                    forwardSlip = (forwardSlip - m_minForwardSlipValue);
                }

                var sidewaySlip = Mathf.Abs(hit.sidewaysSlip);
                if (sidewaySlip < m_minSidewaySlipValue)
                    sidewaySlip = 0;
                else
                    sidewaySlip = (sidewaySlip - m_minSidewaySlipValue);

                amount = Mathf.Max(forwardSlip, sidewaySlip);
            }

            if (m_emiters.ContainsKey(m_currentProperty) && m_emiters[m_currentProperty].HasFloat("_SlipAmount"))
                m_emiters[m_currentProperty].SetFloat("_SlipAmount", amount);
        }

        void Remap_float(float In, Vector2 InMinMax, Vector2 OutMinMax, out float Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }

        VisualEffect CreateEmitter(Transform parent, string name = "")
		{
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var emitter = go.AddComponent<VisualEffect>();
            emitter.gameObject.SetActive(false);
            emitter.transform.localScale = Vector3.one * 0.25f;

            return emitter;
        }

        void ResetSlipAmount(float amount = 0.0f)
        {
            foreach (var emit in m_emiters.Where(emit => emit.Value.HasFloat("_SlipAmount")))
                emit.Value.SetFloat("_SlipAmount", amount);
        }

        /// <summary>
        /// Listen to physical material changes and activate the right Visuel Effect emitter
        /// </summary>
        /// <param name="physicProperty"></param>
        void OnPhysicalMaterialChange(WheelPhysicalProperty physicProperty)
        {
            var prop = GetWheelPhysicalPropertyValue(physicProperty);
            if (m_currentProperty != prop)
            {
                //Stop our old emitter?
                if (m_currentProperty != null)
                    m_emiters[m_currentProperty].Stop();

                m_currentProperty = prop;
                if (m_currentProperty != null)
                {
                    if (!m_emiters.ContainsKey(prop))
                    {
                        VisualEffect emitter = null;
                        if (m_emiterPool.Count > 0)
                            emitter = m_emiterPool.Pop();
                        else
                            emitter = CreateEmitter(m_parent, prop.name);

                        emitter.name = prop.name;
                        emitter.gameObject.SetActive(true);
                        var vfxInfo = GetVfxInfo(PhysicalVFXInfo.Type.TireSmoke, prop);
                        if (vfxInfo != null)
                            emitter.visualEffectAsset = vfxInfo.m_effect;
                        m_emiters.Add(prop, emitter);
                    }

                    m_emiters[m_currentProperty].Play();
                }
            }
        }

        PhysicalVFXInfo GetVfxInfo(PhysicalVFXInfo.Type type, PhysicalProperty physicalProperty)
        {
            if (physicalProperty == null || physicalProperty.m_VFXInfos == null || physicalProperty.m_VFXInfos.Count == 0)
                return null;

            return physicalProperty.m_VFXInfos.FirstOrDefault(prop => prop.m_type == type);
        }

        PhysicalProperty GetWheelPhysicalPropertyValue(WheelPhysicalProperty physicProperty)
        {
            switch (m_wheel)
            {
                case WheelPhysicalProperty.Wheel.FL: return physicProperty.fL;
                case WheelPhysicalProperty.Wheel.FR: return physicProperty.fR;
                case WheelPhysicalProperty.Wheel.RL: return physicProperty.rL;
                case WheelPhysicalProperty.Wheel.RR: return physicProperty.rR;
            };

            return null;
        }
    }
}

