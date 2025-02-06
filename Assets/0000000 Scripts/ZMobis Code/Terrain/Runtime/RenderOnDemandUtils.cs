using com.unity.testtrack.utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RenderOnDemandUtils
{
	public class RenderMeshPass
	{
		public Mesh m_mesh;
		public Material m_material;
		public Transform m_transform;

		public RenderMeshPass(Mesh mesh, Material material, Transform transform)
		{
			m_mesh = mesh;
			m_material = material;
			m_transform = transform;
		}
	}

	public interface IRenderOnDemand
	{
		public List<RenderMeshPass> meshes { get; }
	}
	public class GameObjectRenderOnDemand : IRenderOnDemand
	{
		public GameObjectRenderOnDemand(GameObject[] objects, Material material = null)
		{
			var lmaterial = material;

			m_meshes = new List<RenderMeshPass>();
			foreach (var obj in objects)
			{
				var lodGroup = obj.GetComponent<LODGroup>();
				var meshFilter = obj.GetComponent<MeshFilter>();
				var meshRenderer = obj.GetComponent<MeshRenderer>();
				if (lodGroup != null)
				{
					var lods = lodGroup.GetLODs();
					var lod = 0;//lods.Length - 1;
					lod = Mathf.Clamp(lod, 0, lods.Length - 1);

					var renderer = lods[lod].renderers[0];
					meshFilter = renderer.GetComponent<MeshFilter>();
				}

				if (meshFilter != null)
					m_meshes.Add(new RenderMeshPass(meshFilter.sharedMesh, material != null ? material : meshRenderer.sharedMaterial, meshFilter.transform));
			}		
		}

		//Material m_material;
		//public Material material 
		//{
		//	get 
		//	{
		//		if (m_material == null)
		//			m_material = new Material(Shader.Find("Unlit/DefaultMeshSplatting"));
		//		return m_material;
		//	}	
		//}

		List<RenderMeshPass> m_meshes;
		public List<RenderMeshPass> meshes => m_meshes;
	}

	static void SetRenderTarget(RenderTexture renderTexture, out RenderTexture old)
	{
		old = RenderTexture.active;
		RenderTexture.active = renderTexture;
	}

	static void PushMatrix(Matrix4x4 projectionMatrix, Matrix4x4 modelViewMatrix)
	{
		// Push the projection matrix
		GL.PushMatrix();
		GL.LoadProjectionMatrix(projectionMatrix);
		GL.modelview = modelViewMatrix;

		// It seems that the faces are in a wrong order, so we need to flip them
		GL.invertCulling = true;
	}

	static void PopMatrix()
	{
		// Pop the projection matrix to set it back to the previous one
		GL.PopMatrix();

		// Revert culling
		GL.invertCulling = false;
	}

	static void RenderMesh(Mesh mesh, Matrix4x4 modelMatrix)
	{
		// Draw the mesh!
		Graphics.DrawMeshNow(mesh, modelMatrix);
	}

	static void SetupMaterial(Material mat)
	{
		// Set material as "active". Without this, Unity editor will freeze.
		//mat.SetColor("_Color", color);
		//var forwardPass = mat.FindPass("SceneSelectionPass");
		//if (forwardPass != -1)
		//	pass = forwardPass;
		mat.SetPass(0);
	}

	static Matrix4x4 GetProjectionMatrix(Camera camera)
	{
		var projectionMatrix = camera.projectionMatrix;

		//// If Camera.current is set, multiply our matrix by the inverse of its view matrix
		//if (Camera.current != null)
		//	projectionMatrix = projectionMatrix * Camera.current.worldToCameraMatrix.inverse;

		return projectionMatrix;
	}

	static Matrix4x4 GetModelViewMatrix(Camera cam)
	{
		return cam.worldToCameraMatrix;
	}

	static Matrix4x4 GetModelMatrix(Transform transform)
	{
		//var diff = obj.transform.position - Terrain.activeTerrain.transform.position;
		//return Matrix4x4.TRS(diff, obj.transform.rotation, obj.transform.localScale);
		return transform.localToWorldMatrix;
	}

	static void MakeTopDownCamera(GameObject[] gos, Camera cam)
	{
		if (gos == null || gos.Length == 0)
			return;

		if (!gos.ComputeBounds(out var bb, true))
			return;

		MakeTopDownCamera(bb, cam);
	}

	static void MakeTopDownCamera(Bounds bb, Camera cam)
	{
		//Find the max size of the bounding box 
		//TODO: de-hardcode this.
		var maxSize = Mathf.Max(bb.size.x, bb.size.z) / 2.0f;

		cam.aspect = bb.size.x / bb.size.z;
		cam.orthographic = true;
		cam.orthographicSize = maxSize / cam.aspect;
		cam.transform.SetPositionAndRotation(
			new Vector3(bb.center.x, bb.max.y + 1000, bb.center.z),
			Quaternion.Euler(90, 0, 0));
		cam.nearClipPlane = 0.0001f;
		cam.farClipPlane = bb.size.y + 2000;
	}

	private static Camera m_cameraCache = null;
	public static void Paint(IRenderOnDemand def, RenderTexture renderTexture, Bounds bounds, bool Clear = true)
	{
		if (renderTexture == null || def.meshes == null)
			return;

		if (m_cameraCache == null)
		{
			var go = GameObject.Find("RenderOnDemandUtils_TopDownCam");
			if (go == null)
			{
				go = new GameObject();
				go.name = "RenderOnDemandUtils_TopDownCam";
				go.hideFlags = HideFlags.HideAndDontSave;
				go.AddComponent<Camera>();
				go.SetActive(false);
			}
			m_cameraCache = go.GetComponent<Camera>();
		}

		MakeTopDownCamera(bounds, m_cameraCache);

		SetRenderTarget(renderTexture, out var prevRT);
		if (Clear)
			GL.Clear(true, true, Color.clear);
		PushMatrix(GetProjectionMatrix(m_cameraCache), GetModelViewMatrix(m_cameraCache));

		foreach (var mesh in def.meshes.Where(mesh => mesh.m_transform != null))
		{
			if (mesh.m_material == null)
				continue;
			if (mesh.m_mesh == null)
			{
				Debug.LogError("Cannot Render a null mesh in GameObject: " + mesh.m_transform.name);
				continue;
			}

			SetupMaterial(mesh.m_material);
			RenderMesh(mesh.m_mesh, GetModelMatrix(mesh.m_transform));
		}

		PopMatrix();
		SetRenderTarget(prevRT, out var tmp);
	}

	public static RenderTextureDescriptor GetDescriptor(Texture2D texture, UnityEngine.Rendering.TextureDimension dimension = UnityEngine.Rendering.TextureDimension.Tex2D, int volumeDepth = 1)
	{
		RenderTextureDescriptor descriptor = new RenderTextureDescriptor
		{
			height = texture.height,
			width = texture.width,
			volumeDepth = volumeDepth,
			enableRandomWrite = true,
			graphicsFormat = texture.graphicsFormat, // we need 16 bit precision for the distance field
			useMipMap = true,
			msaaSamples = 1,
			mipCount = texture.mipmapCount,
			autoGenerateMips = true,
			dimension = dimension,
		};

		return descriptor;
	}

	public static RenderTexture GetTemporary(RenderTextureDescriptor desc)
	{
		return RenderTexture.GetTemporary(desc);
	}

	public static void ReleaseTemporary(RenderTexture temp)
	{
		RenderTexture.ReleaseTemporary(temp);
	}

	public static void Clear(RenderTexture rt, Color color)
	{
		SetRenderTarget(rt, out var prevRT);
		GL.Clear(true, true, color);
		SetRenderTarget(prevRT, out var tmp);
	}
}