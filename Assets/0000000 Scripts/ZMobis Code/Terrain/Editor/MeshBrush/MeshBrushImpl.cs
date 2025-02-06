using com.unity.testtrack.partioning.grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TerrainTools;

namespace com.unity.testtrack.terrainsystem
{
	//NB. using the stamp tool is not precise enough for large meshes, so we use racasts to build a "point cloud" representation of the mesh instead
	internal class SmoothBrushOnPaint : IOnPaint
	{
		private Texture m_brushTexture;
		public Texture brushTexture => m_brushTexture;

		public Vector2 uv => throw new NotImplementedException();

		public float brushStrength => throw new NotImplementedException();

		public float brushSize => throw new NotImplementedException();

		public bool hitValidTerrain => throw new NotImplementedException();

		public RaycastHit raycastHit => throw new NotImplementedException();

		public void Repaint(RepaintFlags flags = RepaintFlags.UI)
		{
			throw new NotImplementedException();
		}

		public void RepaintAllInspectors()
		{
			throw new NotImplementedException();
		}

		public SmoothBrushOnPaint(Texture brushTexture)
		{
			m_brushTexture = brushTexture;
		}
	}
	internal class CustomBrushSmoother : IBrushSmoothController
	{
		public int kernelSize { get; set; }

		Material m_Material = null;
		Material GetMaterial()
		{
			if (m_Material == null)
				m_Material = new Material(Shader.Find("Hidden/TerrainTools/SmoothHeight"));
			return m_Material;
		}

		public CustomBrushSmoother(string name)
		{
			// intentionally blank -- reserved for future customization
		}

		public bool active { get { return Event.current.shift; } }

		public void OnEnterToolMode() { }
		public void OnExitToolMode() { }
		public void OnSceneGUI(Terrain terrain, IOnSceneGUI editContext)
		{
			// intentionally blank -- reserved for future customization
		}

		public void OnInspectorGUI(Terrain terrain, IOnInspectorGUI editContext)
		{
			// for future customization, to select different blur tools, etc...
		}

		public bool OnPaint(Terrain terrain, IOnPaint editContext, float brushSize, float brushRotation, float brushStrength, Vector2 uv)
		{
			Profiler.BeginSample("CustomBrushSmoother.OnPaint");

			BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, uv, brushSize, brushRotation);
			PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds());

			Material mat = GetMaterial();//TerrainPaintUtility.GetBuiltinPaintMaterial();

			float m_direction = 0.0f; //TODO: UI for this

			Vector4 brushParams = new Vector4(brushStrength, 0.0f, 0.0f, 0.0f);
			mat.SetTexture("_BrushTex", editContext.brushTexture);
			mat.SetVector("_BrushParams", brushParams);
			Vector4 smoothWeights = new Vector4(
				Mathf.Clamp01(1.0f - Mathf.Abs(m_direction)),   // centered
				Mathf.Clamp01(-m_direction),                    // min
				Mathf.Clamp01(m_direction),                     // max
				0);
			mat.SetInt("_KernelSize", (int)Mathf.Max(1, kernelSize)); // kernel size
			mat.SetVector("_SmoothWeights", smoothWeights);

			var texelCtx = Utility.CollectTexelValidity(paintContext.originTerrain, brushXform.GetBrushXYBounds(), 1);
			Utility.SetupMaterialForPaintingWithTexelValidityContext(paintContext, texelCtx, brushXform, mat);

			paintContext.sourceRenderTexture.wrapMode = TextureWrapMode.Clamp;

			var temp = RTUtils.GetTempHandle(paintContext.destinationRenderTexture.descriptor);
			temp.RT.wrapMode = TextureWrapMode.Clamp;
			mat.SetVector("_BlurDirection", Vector2.right);
			Graphics.Blit(paintContext.sourceRenderTexture, temp, mat);
			mat.SetVector("_BlurDirection", Vector2.up);
			Graphics.Blit(temp, paintContext.destinationRenderTexture, mat);

			TerrainPaintUtility.EndPaintHeightmap(paintContext, "Terrain Paint - Smooth Height");

			texelCtx.Cleanup();
			RTUtils.Release(temp);

			Profiler.EndSample();
			return true;
		}
	}

	public class MeshBrushImpl : MonoBehaviour
	{
		public delegate void Cleanup();

		[System.Serializable]
		public struct PaintSelectionOptions
		{
			public float strength;// { get; set; }
			public float dir;// { get; set; }
			public Transform[] selection;
			public AlphaSplatDefinition splatDefinition;
			public bool includeChildren;
			public float smoothRadius;
			public float heightOffset;

			public PaintSelectionOptions(float strength, float dir, Transform[] selection, bool includeChildren, AlphaSplatDefinition splatDefinition = null)
			{
				this.strength = strength;
				this.dir = dir;
				this.selection = selection;
				this.splatDefinition = splatDefinition;
				this.includeChildren = includeChildren;
				this.smoothRadius = 0;
				this.heightOffset = 0;
			}

			public PaintSelectionOptions(float strength, float dir, Transform[] selection, bool includeChildren, float smoothRadius, float heightOffset, AlphaSplatDefinition splatDefinition = null)
			{
				this.strength = strength;
				this.dir = dir;
				this.selection = selection;
				this.splatDefinition = splatDefinition;
				this.includeChildren = includeChildren;
				this.smoothRadius = smoothRadius;
				this.heightOffset = heightOffset;
			}
		}

		public class ColliderInfo
		{
			public Collider collider;
			public Bounds infiniteBounds;
			public Vector2 cellSize;
			public SurfaceInfo[,] surface;
			public Cleanup cleanup;
			public Vector2 minMax;

			public ColliderInfo(Collider collider, Bounds infiniteBounds, Cleanup cleanup = null, SurfaceInfo[,] surface = null)
			{
				this.collider = collider;
				this.infiniteBounds = infiniteBounds;
				this.cleanup = cleanup;
				this.surface = surface;
				this.cellSize = Vector2.zero;
			}
		}

		public class SurfaceInfo : CellData
		{
			public bool isOnMesh;
			public Vector3 hitLocation;
			public Bounds region;
			public Vector3 closestContributorHit;
			public float closestContributorPourcent;
			//public List<Tuple<float, float>> contributions = new List<Tuple<float, float>>();

			public SurfaceInfo(Vector3 center, Vector3 size, Vector3 hitLocation)
			{
				this.hitLocation = hitLocation;
				region = new Bounds(center, size);
			}

			public Vector3 position { get => region.center; set => throw new NotImplementedException(); }
			public Bounds Bounds { get => region; set => throw new NotImplementedException(); }
		}

		static private PartitionGrid m_surfacePartitionning;

		public static MeshRenderer[] GetMeshRenderersFromSelection(PaintSelectionOptions options)
		{
			var selections = options.selection;//Selection.GetFiltered<Transform>(SelectionMode.TopLevel);

			//TODO: filter LOD's
			//Do we have a meshRenderer in our selection?
			List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
			foreach (var selection in selections)
			{
				var renderers = options.includeChildren ? selection.GetComponentsInChildren<MeshRenderer>() : selection.GetComponents<MeshRenderer>();
				meshRenderers.AddRange(renderers);
			}
			return meshRenderers.ToArray();
		}

		private static Terrain[] GetTerrains()
		{
			return Terrain.activeTerrains;
		}

		private static Terrain[] GetIntersectinTerrains(Bounds bounds)
		{
			var terrains = GetTerrains();
			List<Terrain> collidingTerrains = new List<Terrain>();
			foreach (var terrain in terrains)
			{
				var terrainData = terrain.terrainData;
				var terrainTransform = terrain.transform;
				var terrainBounds = new Bounds(terrainTransform.localToWorldMatrix.MultiplyPoint3x4(terrainData.bounds.center), terrainData.bounds.size);

				if (terrainBounds.Intersects(bounds))
					collidingTerrains.Add(terrain);
					
			}

			return collidingTerrains.ToArray();
		}

		public static void PaintSelection(PaintSelectionOptions options)
		{
			var renderers = GetMeshRenderersFromSelection(options);
			var terrains = GetTerrains();

			if (!Validate(renderers, terrains))
				return;


			var collidersInfo = GetColliders(renderers);
			foreach (var colliderInfo in collidersInfo)
			{
				try
				{
					var collidingTerrains = GetIntersectinTerrains(colliderInfo.infiniteBounds);

					foreach (var terrain in collidingTerrains)
						PaintObject(terrain, colliderInfo, options);
				}
				finally
				{
					if (colliderInfo.cleanup != null)
						colliderInfo.cleanup();
				}
			}
		}

		private static bool Validate(MeshRenderer[] renderers, Terrain[] terrains)
		{
			if (renderers == null || renderers.Length == 0 ||
				terrains == null || terrains.Length == 0)
			{
				Debug.LogWarning("There is no " + typeof(MeshRenderer).Name + " in the slection or there is no terrain in the scene");
				return false;
			}

			return true;
		}

		public static ColliderInfo[] GetColliders(MeshRenderer[] renderers)
		{
			List<ColliderInfo> colliders = new List<ColliderInfo>();
			foreach (var r in renderers)
			{
				bool bWasActive = r.gameObject.activeSelf;
				r.gameObject.SetActive(true);
				var collider = r.GetComponent<Collider>();
				Cleanup cleanUp = null;
				if (collider == null)
				{
					collider = r.gameObject.AddComponent<MeshCollider>();
					cleanUp = () =>
					{
						GameObject.DestroyImmediate(collider);
						r.gameObject.SetActive(bWasActive);
					};
				}

				var colliderInfiniteBounds = new Bounds(collider.bounds.center, new Vector3(collider.bounds.size.x, float.MaxValue, collider.bounds.size.z));
				colliders.Add(new ColliderInfo(collider, colliderInfiniteBounds, cleanUp));
			}

			return colliders.ToArray();
		}

		private static float[,] GetHeights(TerrainData terrain, out int maxHeight, out int maxLength)
		{
			float[,] heights = terrain.GetHeights(0, 0, terrain.heightmapResolution, terrain.heightmapResolution);
			maxHeight = heights.GetLength(0);
			maxLength = heights.GetLength(1);

			return heights;
		}

		static void PaintHeightField(Terrain terrainObj, ColliderInfo collider, PaintSelectionOptions options)
		{
			var terrainData = terrainObj.terrainData;
			var terrainPosition = terrainObj.transform.position;
			var terrainSize = terrainData.size;

			var heights = GetHeights(terrainData, out var maxHeight, out var maxLength);


			var t = collider.surface;
			var xCount = t.GetLength(0);
			var zCount = t.GetLength(1);
			for (int x = 0; x < xCount; x++)
			//Parallel.For(0, xCount, (x) =>
			{
				ShowProgressBar(x, xCount, "Writing surface data in the Heightfield: " + terrainObj.name);
				//for (int z = 0; z < zCount; z++)
				Parallel.For(0, zCount, (z) =>
				{
					if (!float.IsInfinity(t[x, z].hitLocation.sqrMagnitude))
					{
						var height = ((t[x, z].hitLocation.y - terrainPosition.y) / terrainSize.y) * options.strength;
						var coord = ConvertToTerrainMapCoord(t[x, z].hitLocation, terrainPosition, terrainSize, maxHeight, maxLength);

						//Set the defautl layer contribution
						if (coord.y >= 0 && coord.y < maxHeight &&
								coord.x >= 0 && coord.x < maxLength)
						{
							heights[(int)coord.y, (int)coord.x] = height - options.heightOffset;
						}
					}
				});
			}//);

			Undo.RegisterCompleteObjectUndo(terrainData, terrainData.name);
			terrainData.SetHeights(0, 0, heights);

			ShowProgressBar(100, 100, "Writing surface data in the Heightfield: " + terrainObj.name);
		}

		private static void PaintObject(Terrain terrainObj, ColliderInfo collider, PaintSelectionOptions options)
		{
			var terrainData = terrainObj.terrainData;

			try
			{
				ShowProgressBar(0, 100, "Calculating object surface: " + collider.collider.name);

				var resolutionWidth = options.splatDefinition != null ? terrainData.heightmapResolution : Mathf.Max(terrainData.heightmapResolution, terrainData.alphamapResolution);
				var resolutionHeight = resolutionWidth;

				var fromBottom = options.dir != -1;
				var fromTop = !fromBottom;


				CalculateNumberSubdivision(collider.collider, resolutionWidth/terrainData.size.x, resolutionHeight/terrainData.size.z, 10, out var widthSubdivisions, out var heightSubdivisions);
				if (collider.surface == null || collider.surface.GetLength(0) != widthSubdivisions+1 ||
					collider.surface.GetLength(1) != heightSubdivisions+1)
				{ 
					List<Ray> rays = new List<Ray>();
					collider.surface = GetSurfaceLocations(collider.collider, resolutionWidth / terrainData.size.x, resolutionHeight / terrainData.size.z, ref rays, out var cellSize, out var minMax, fromTop);
					collider.cellSize = cellSize;
					collider.minMax = minMax;

					Profiler.BeginSample("m_surfacePartitionning");
					//Partition our points
					var bounds = collider.collider.bounds;
					bounds.Expand(10);
					m_surfacePartitionning = new PartitionGrid(bounds.min, bounds.size, 50, 50 /*c.surface.GetLength(0), c.surface.GetLength(1)*/, false);
					m_surfacePartitionning.Insert(collider.surface.Cast<SurfaceInfo>().ToArray());
					Profiler.EndSample();

					CalculateContributions(collider, options.smoothRadius);
				}

				PaintHeightField(terrainObj, collider, options);
				SmoothHeightField(terrainObj, collider, options);
			}
			finally
			{
				HideProgressBar();

				terrainObj.Flush();
			}
		}

		//Add the distance to the closest point on spline
		//and smooth the point based on a radius
		static public void CalculateContributions(ColliderInfo colliderInfo, float blendRadius = 10.0f)
		{
			Profiler.BeginSample("CalculateContributions");

			var t = colliderInfo.surface;
			var xCount = t.GetLength(0);
			var zCount = t.GetLength(1);
			var searchRadius = blendRadius * 1.5f;
			var searchDiameter = blendRadius * 1.5f;
			var diameter = blendRadius * 2;

			//Parallel.For(0, xCount, (x) =>
			for (int x = 0; x < xCount; x++)
			{
				ShowProgressBar(x, xCount, "Calculating object Contributions: " + colliderInfo.collider.name);

				Parallel.For(0, zCount, (z) =>
				//for (int z = 0; z < zCount; z++)
				{
					if (!float.IsInfinity(t[x, z].hitLocation.sqrMagnitude) && t[x, z].isOnMesh)
					{
						Bounds b = new Bounds(t[x, z].hitLocation, new Vector3(blendRadius, float.PositiveInfinity, blendRadius));
						Profiler.BeginSample("FindCells");
						var cells = m_surfacePartitionning.FindCells(t[x, z].hitLocation, searchRadius);
						Profiler.EndSample();
						var hitLoc = new Vector2(b.center.x, b.center.z);

						Profiler.BeginSample("iterate on cells");
						Parallel.ForEach(cells, cell =>
						//foreach (var cell in cells.Where(cell => cell.hasData))
						{
							foreach (SurfaceInfo cData in cell.data.Where(cData => !(cData as SurfaceInfo).isOnMesh))
							{
								var loc = new Vector2(cData.region.center.x, cData.region.center.z);
								var dist = Vector2.Distance(hitLoc, loc);
								if (dist < searchDiameter)
								{
									var invertDistance = 1 - (dist / diameter);
									invertDistance = Mathf.Max(0, invertDistance);
									if (cData.closestContributorPourcent == 0 || cData.closestContributorPourcent > invertDistance)
									{
										cData.closestContributorPourcent = invertDistance;
										cData.closestContributorHit = t[x, z].hitLocation;
									}
								}
							}
						});
						Profiler.EndSample();
					}
				});
			}//);

			Profiler.EndSample();
		}

		static float[] GetLayerContributions(TerrainLayer[] layers, AlphaSplatDefinition def)
		{
			float[] contributions = new float[layers.Length];

			float totalContribution = 0;
			for (int layer = contributions.Length - 1; layer > 0; layer--)
			{
				if (layers[layer] == null)
				{
					contributions[layer] = 0;
				}
				else
				{
					var contrib = def.GetContribution(layers[layer].name);
					contributions[layer] = contrib;
					totalContribution += contrib;
				}
			}

			//Default layer have the remaining
			contributions[0] = Mathf.Clamp(1.0f - totalContribution, 0, 1.0f);

			return contributions;
		}

		static Vector2 ConvertToTerrainMapCoord(Vector3 srcPos, Vector3 terrainPos, Vector3 terrainSize, float mapWidth, float mapHeight)
		{
			// get the normalized position of this game object relative to the terrain
			Vector3 tempCoord = (srcPos - terrainPos);
			Vector3 coord;
			coord.x = tempCoord.x / terrainSize.x;
			coord.y = tempCoord.y / terrainSize.y;
			coord.z = tempCoord.z / terrainSize.z;

			// get the position of the terrain alpha maps where this game object is
			return new Vector2((int)(coord.x * (float)mapWidth), (int)(coord.z * (float)mapHeight));
		}

		static void CalculateNumberSubdivision(Collider collider, float samplePerMeterWidth, float samplePerMeterHeight, float expensionZone, out int widthSubdivisions, out int heightSubdivisions)
		{
			widthSubdivisions = 0;
			heightSubdivisions = 0;

			var bounds = collider.bounds;
			bounds.Expand(expensionZone);

			//Calculate the number of sample in the object
			//We set a minimum to make sure we have at least one sample in the middle of the object
			widthSubdivisions = Mathf.Max(Mathf.CeilToInt(bounds.size.x * samplePerMeterWidth), 2);
			heightSubdivisions = Mathf.Max(Mathf.CeilToInt(bounds.size.z * samplePerMeterHeight), 2);
		}

		static public SurfaceInfo[,] GetSurfaceLocations(Collider collider, float samplePerMeterWidth, float samplePerMeterHeight, ref List<Ray> rays, out Vector2 cellSize, out Vector2 minMax, bool top = false)
		{
			cellSize = Vector2.zero;
			minMax = Vector2.zero;
			rays?.Clear();

			if (collider == null)
				return null;

			SurfaceInfo[,] infos = null;
			//Vector3[,] loc = null;
			var bounds = collider.bounds;
			bounds.Expand(10);
			CalculateNumberSubdivision(collider, samplePerMeterWidth, samplePerMeterHeight, 10, out var widthSubdivisions, out var heightSubdivisions);

			//loc = new Vector3[widthSubdivisions + 1, heightSubdivisions + 1];
			infos = new SurfaceInfo[widthSubdivisions + 1, heightSubdivisions + 1];
			Ray ray = new Ray(new Vector3(bounds.min.x, bounds.min.y - bounds.size.y, bounds.min.z), Vector3.up);
			if (top)
				ray = new Ray(new Vector3(bounds.min.x, bounds.max.y + bounds.size.y, bounds.min.z), -Vector3.up);

			RaycastHit hit = new RaycastHit();
			cellSize = new Vector2(bounds.size.x / widthSubdivisions, bounds.size.z / heightSubdivisions);
			Vector3 rayOrigin = ray.origin;

			float min = float.PositiveInfinity;
			float max = float.NegativeInfinity;
			for (int x = 0; x <= widthSubdivisions; x++)
			{
				ShowProgressBar(x, widthSubdivisions, "Calculating object surface: " + collider.name);
				for (int z = 0; z <= heightSubdivisions; z++)
				{
					//rays?.Add(new Ray(new Vector3(ray.origin.x, bounds.max.y, ray.origin.z), ray.direction));

					infos[x, z] = new SurfaceInfo(ray.origin, new Vector3(cellSize.x, float.PositiveInfinity, cellSize.y), Vector3.negativeInfinity);
					//loc[x, z] = Vector3.negativeInfinity;
					if (collider.Raycast(ray, out hit, float.MaxValue))
					{
						//loc[x, z] = hit.point;
						infos[x, z].hitLocation = hit.point;
						infos[x, z].isOnMesh = true;

						if (hit.point.y < min)
							min = hit.point.y;
						if (hit.point.y > max)
							max = hit.point.y;
						//When we hit, we need to propagate this value to the rest of the points if we do blending until our influence read 0
					}

					rayOrigin.z += cellSize[1];
					ray.origin = rayOrigin;
				}

				rayOrigin.x += cellSize[0];
				rayOrigin.z = bounds.min.z;
				ray.origin = rayOrigin;
			}

			minMax.Set(min, max);
			return infos;
		}

		static void SmoothHeightField(Terrain terrainObj, ColliderInfo collider, PaintSelectionOptions options)
		{
			if (options.smoothRadius <= 0)
				return;

			Profiler.BeginSample("SmoothHeightField");

			var terrainData = terrainObj.terrainData;
			var terrainPosition = terrainObj.transform.position;
			var terrainSize = terrainData.size;

			GetHeights(terrainData, out var maxHeight, out var maxLength);

			var t = collider.surface;
			var xCount = t.GetLength(0);
			var zCount = t.GetLength(1);

			CustomBrushSmoother smoother = new CustomBrushSmoother("");
			var brushTexture = EditorGUIUtility.Load("builtin_brush_1.tif") as Texture;
			var context = new SmoothBrushOnPaint(brushTexture);
			smoother.kernelSize = 10;
			for (int x = 0; x < xCount; x++)
			{
				ShowProgressBar(x, xCount, "Smoothing surface data in the Heightfield: " + terrainObj.name);

				for (int z = 0; z < zCount; z++)
				{
					var coord = ConvertToTerrainMapCoord(t[x, z].region.center, terrainPosition, terrainSize, maxHeight, maxLength);
					if (coord.y < 0 || coord.y >= maxHeight ||
						coord.x < 0 || coord.x >= maxLength)
					{
						continue;
					}

					if (!t[x, z].isOnMesh && t[x, z].closestContributorPourcent > 0)
						smoother.OnPaint(terrainObj, context, options.smoothRadius * (1 - t[x, z].closestContributorPourcent), 0, 1, new Vector2(coord.x / maxHeight, coord.y / maxLength));
				}
			}

			ShowProgressBar(100, 100, "Smoothing surface data in the Heightfield: " + terrainObj.name);

			Profiler.EndSample();
		}

		public static void ShowProgressBar(float progress, float maxProgress, string str)
		{

			float p = progress / maxProgress;
			EditorUtility.DisplayProgressBar(str, Mathf.RoundToInt(p * 100f) + " %", p);
		}
		public static void HideProgressBar()
		{
			EditorUtility.ClearProgressBar();
		}
	}
}