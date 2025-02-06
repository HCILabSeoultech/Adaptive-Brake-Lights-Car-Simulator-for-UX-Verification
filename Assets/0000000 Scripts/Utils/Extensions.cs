using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace com.unity.testtrack.utils
{
	public static class ArrayExtension
	{
		public static bool ComputeBounds(this Renderer[] renderers, out Bounds bounds, bool includeChildren = false)
		{
			bounds = new Bounds();
			for (int idxRenderer = 0; renderers != null & idxRenderer < renderers.Length; idxRenderer++)
			{
				Renderer renderer = renderers[idxRenderer];
				if (idxRenderer == 0)
					bounds = renderer.bounds;
				else
					bounds.Encapsulate(renderer.bounds);


				//The game object component is the first in the list, do just do the first one in this case
				if (!includeChildren)
					break;
			}
			return renderers != null && renderers.Length > 0;
		}

		public static bool ComputeBounds(this Terrain[] terrains, out Bounds bounds, bool includeChildren = false)
		{
			bounds = new Bounds();
			for (int idxRenderer = 0; terrains != null & idxRenderer < terrains.Length; idxRenderer++)
			{
				Terrain terrain = terrains[idxRenderer];
				var tb = terrain.terrainData.bounds;

				if (idxRenderer == 0)
					bounds = tb;
				else
					bounds.Encapsulate(new Bounds(terrain.transform.localToWorldMatrix.MultiplyPoint3x4(tb.center), tb.size));

				//The game object component is the first in the list, do just do the first one in this case
				if (!includeChildren)
					break;
			}
			return terrains != null && terrains.Length > 0;
		}
	}

	public static class GameObjectsExtensions
	{
		public static bool ComputeBounds(this GameObject[] gos, out Bounds bounds, bool includeChildren)
		{
			bounds = new Bounds(gos[0].transform.position, Vector3.zero);
			bool hasValidBounds = false;
			foreach (var go in gos)
			{
				if (go.ComputeBounds(out var b, includeChildren))
				{
					bounds.Encapsulate(b);
					hasValidBounds = true;
				}
			}

			return hasValidBounds;
		}

		public static bool ComputeBounds(this GameObject go, out Bounds bounds, bool includeChildren)
		{
			bounds = new Bounds(go.transform.position, Vector3.zero);
			bool hasValidBounds = false;

			Terrain[] t = go.GetComponentsInChildren<Terrain>();
			if (t.ComputeBounds(out var tb, includeChildren))
			{
				//The terrain bounds is not tranformed and it cannot scale of rotate
				bounds = new Bounds(go.transform.position + tb.center, tb.size);
				hasValidBounds = true;
			}

			Renderer[] r = go.GetComponentsInChildren<Renderer>();
			if (r.ComputeBounds(out var b, includeChildren))
			{
				if (hasValidBounds)
					bounds.Encapsulate(b);
				else
					bounds = b;
				hasValidBounds = true;
			}

			return hasValidBounds;
		}
	}

	static class HelperExtensions
	{
		public static Texture2D ToTexture2D(this RenderTexture renderTexture)
		{
			if (renderTexture == null) return null;

			var myTexture2D = new Texture2D(renderTexture.width, renderTexture.height, renderTexture.graphicsFormat, TextureCreationFlags.None);

			var active = RenderTexture.active;
			RenderTexture.active = renderTexture;
			myTexture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);
			myTexture2D.Apply();

			RenderTexture.active = active;

			return myTexture2D;
		}
	}
}

