using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.unity.testtrack.terrainsystem
{
	public class ObjectPool<T> where T : new()
	{
		Stack<T> stack = new Stack<T>();
		object objLock = new object();

		public ObjectPool()
		{
		}

		public ObjectPool(int initialCount, Action<T> OnCreated = null)
		{
			lock (objLock)
			{
				for (int i = 0; i < initialCount; i++)
				{
					stack.Push(new T());
					OnCreated?.Invoke(stack.Peek());
				}
			}
		}

		public T Acquire(Action<T> OnAcquire = null)
		{
			T val;
			lock (objLock)
			{
				if (stack.Count == 0)
					stack.Push(new T());

				val = stack.Pop();
			}

			OnAcquire?.Invoke(val);
			return val;
		}

		public void Realease(T data)
		{
			lock (objLock)
			{
				stack.Push(data);
			}
		}

		public void Clear()
		{
			lock (objLock)
			{
				stack.Clear();
			}
		}
	}

	public static class ScatteringRulesUtils
	{
#region Coordonate Transforms
		public static Vector3 WorldToTerrain(Vector3 pos, Vector3 terrainSize)
		{
			pos.Set(pos.x / terrainSize.x, pos.y / terrainSize.y, pos.z / terrainSize.z);
			return pos;
		}

		public static Vector3 TerrainToWorld(Vector3 pos, Vector3 terrainSize)
		{
			pos.Set(pos.x * terrainSize.x, pos.y * terrainSize.y, pos.z * terrainSize.z);
			return pos;
		}

		public static float ToUV(float val, Vector3 terrainSize)
		{
			return val / terrainSize.x;
		}

		public static float ToTerrain(float val, Vector3 terrainSize)
		{
			return val * terrainSize.x;
		}
#endregion


		public static void GatherRules(ScatteringRule root, ref List<ScatteringRule> foundRules, bool errorOnLoops)
		{
			if (root == null)
				return;

			//Check for loops
			if (foundRules.Contains(root))
			{
				if (errorOnLoops)
					Debug.LogError("Error: Loop detected in rule: " + foundRules[0] + " Caused by Sub-Rule: " + root);
				return;
			}

			foundRules.Add(root);
			foreach (var r in root.rules)
			{
				foreach (var p in r.prefabs)
				{
					if (p == null || p.prefab == null)
					{
						Debug.LogError("Missing prefab in a sub-rule of : " + root.gameObject);
						continue;
					}

					GatherRules(p.prefab.GetComponent<ScatteringRule>(), ref foundRules, errorOnLoops);
				}
			}
		}
	}

	internal class BrushRep
	{
		private const int kMinBrushSize = 3;

		private int m_Size;
		private float[] m_Strength;
		private Texture2D m_OldBrushTex;

		public float GetStrengthInt(int ix, int iy)
		{
			ix = Mathf.Clamp(ix, 0, m_Size - 1);
			iy = Mathf.Clamp(iy, 0, m_Size - 1);

			float s = m_Strength[iy * m_Size + ix];

			return s;
		}

		public void CreateFromBrush(Texture2D brushTex, int size)
		{
			if (size == m_Size && m_OldBrushTex == brushTex && m_Strength != null)
				return;

			Texture2D mask = brushTex;
			if (mask != null)
			{
				Texture2D readableTexture = null;
				if (!mask.isReadable)
				{
					readableTexture = new Texture2D(mask.width, mask.height, mask.format, mask.mipmapCount > 1);
					Graphics.CopyTexture(mask, readableTexture);
					readableTexture.Apply();
				}
				else
				{
					readableTexture = mask;
				}

				float fSize = size;
				m_Size = size;
				m_Strength = new float[m_Size * m_Size];
				if (m_Size > kMinBrushSize)
				{
					for (int y = 0; y < m_Size; y++)
					{
						float v = y / fSize;
						for (int x = 0; x < m_Size; x++)
						{
							float u = x / fSize;
							m_Strength[y * m_Size + x] = readableTexture.GetPixelBilinear(u, v).r;
						}
					}
				}
				else
				{
					for (int i = 0; i < m_Strength.Length; i++)
						m_Strength[i] = 1.0F;
				}

				if (readableTexture != mask)
					UnityEngine.Object.DestroyImmediate(readableTexture);
			}
			else
			{
				m_Strength = new float[1];
				m_Strength[0] = 1.0F;
				m_Size = 1;
			}

			m_OldBrushTex = brushTex;
		}
	}

	internal static class TempTerrainPaintUtilityEditor
	{
#if UNITY_EDITOR
		private static int s_CurrentOperationUndoGroup = -1;
#endif
		private static Dictionary<UnityEngine.Object, int> s_CurrentOperationUndoStack = new Dictionary<UnityEngine.Object, int>();

		internal static void UpdateTerrainDataUndo(TerrainData terrainData, string undoName)
		{
#if UNITY_EDITOR
			// if we are in a new undo group (new operation) then start with an empty list
			if (Undo.GetCurrentGroup() != s_CurrentOperationUndoGroup)
			{
				s_CurrentOperationUndoGroup = Undo.GetCurrentGroup();
				s_CurrentOperationUndoStack.Clear();
			}

			if (!s_CurrentOperationUndoStack.ContainsKey(terrainData))
			{
				s_CurrentOperationUndoStack.Add(terrainData, 0);
				Undo.RegisterCompleteObjectUndo(terrainData, undoName);
			}
#endif
		}
	}

	internal static class TerrainBrushPreviewMesh
	{
		static Dictionary<int, Mesh> meshCache = new Dictionary<int, Mesh>();
		static public Mesh GetMeshFromRequest(TreeInstanceWrapper request)
		{
			if (meshCache.ContainsKey(request.instance.prototypeIndex))
				return meshCache[request.instance.prototypeIndex];

			Mesh mesh = null;
			var treeProto = request.m_bridge.m_treePrototypes[request.instance.prototypeIndex];
			var instance = treeProto.prefab.gameObject;//GameObject.Instantiate(request.component.gameObject);
													   //instance.hideFlags = HideFlags.HideAndDontSave;

			var lodGroup = instance.GetComponent<LODGroup>();
			if (lodGroup != null)
			{
				var lods = lodGroup.GetLODs();
				var lod = 0;
				lod = Mathf.Clamp(lod, 0, lods.Length - 1);

				var renderer = lods[lod].renderers[0];
				mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
			}
			else
			{
				mesh = instance.GetComponent<MeshFilter>().sharedMesh;
			}

			meshCache.Add(request.instance.prototypeIndex, mesh);
			return mesh;
		}
	}
}