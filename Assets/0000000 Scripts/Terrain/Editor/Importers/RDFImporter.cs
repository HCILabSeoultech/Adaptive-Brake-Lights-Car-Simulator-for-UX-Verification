using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEngine.Profiling;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Globalization;

namespace com.unity.testtrack.terrainsystem.importer
{
    [ScriptedImporter(1, "rdf")]
    class RDFImporter : ScriptedImporter
    {
        public float m_Scale = 1;
        public bool m_SplitSubmeshIntoGameObjects = true;

		#region RDF items
		abstract public class Item
		{
            public abstract void Read(string line, Section section);
        }

		public class StandardItem : Item
		{
            string name;
            string value;

			public override void Read(string line, Section section)
			{
                try
                {
                    var values = line.Trim().Split('=');
                    name = values[0].Trim();
                    value = values[1].Trim('\'', '\"').Trim();
                }
                catch(Exception e)
				{
					UnityEngine.Debug.LogError(e.Message);
				}
            }
		}

		public class NodeItem : Item
        {
            public int id;
            public Vector3 position;

			public override void Read(string line, Section section)
			{
                //Hardcoded for now
                var values = line.Trim().Split(' ');
				id = ConvertToInt(values[0]);
				position.x = float.Parse(values[1]);
				position.z = float.Parse(values[2]);
				position.y = float.Parse(values[3]);
			}
		}

        public class ElementItem : Item
        {
            public int node1Id;
            public int node2Id;
            public int node3Id;
            public int matId;

            public override void Read(string line, Section section)
            {
				//Hardcoded for now
				var values = line.Trim().Split(' ');
				node1Id = ConvertToInt(values[0])-1; //0 based
                node2Id = ConvertToInt(values[1])-1; //0 based
                node3Id = ConvertToInt(values[2])-1; //0 based
                matId = ConvertToInt(values[3])-1; //0 based
			}
        }

        public class MaterialItem : Item
        {
            public int id;
            public float mu;
            public string name;

            public override void Read(string line, Section section)
			{
                var values = line.Trim().Split(' ');
                id = ConvertToInt(values[0]);
                float.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out mu);
                if (values.Length >= 4)
                    name = values[3];
            }
		}

        static public int ConvertToInt(string str)
        {
            var temp = 0;
            for (int i = 0; i < str.Length; i++)
                temp = temp * 10 + (str[i] - '0');

            return temp;
        }
        #endregion

        #region RDF Section
        public class Section
		{
            public string Name;
            public string[] itemIds;
            public List<Item> values = new List<Item>();

            public List<Vector3> buildVerticeList()
			{
                List<Vector3> vertices = new List<Vector3>();
                if (Name == "NODES")
				{
                    foreach (var value in values.Where(val => val != null))
                        vertices.Add(((NodeItem)value).position);
                }

                return vertices;
			}

            public Dictionary<int, List<int>> buildIndexList()
            {
                Dictionary<int, List<int>> indices = new Dictionary<int, List<int>>();
                if (Name == "ELEMENTS")
                {
                    var elementArray = values.OfType<ElementItem>().ToArray();
                    indices = CreateSubmeshesIndicesArray(elementArray);
                }

                return indices;
            }

            private Dictionary<int, List<int>> CreateSubmeshesIndicesArray(ElementItem[] items)
			{
                Dictionary<int, List<int>> indices = new Dictionary<int, List<int>>();

                if (items.Length <= 0)
                    return indices;

                for (int i = 0; i < items.Count(); i++)
                {
                    var item = items[i];
                    if (!indices.ContainsKey(item.matId))
                        indices.Add(item.matId, new List<int>());

                    indices[item.matId].Add(item.node3Id);
                    indices[item.matId].Add(item.node2Id);
                    indices[item.matId].Add(item.node1Id);
                }

                //Create double sided
                for (int i = 0; i < items.Count(); i++)
                {
                    var item = items[i];
                    if (!indices.ContainsKey(item.matId))
                        indices.Add(item.matId, new List<int>());

                    indices[item.matId].Add(item.node1Id);
                    indices[item.matId].Add(item.node2Id);
                    indices[item.matId].Add(item.node3Id);
                }

                return indices;
            }

            private int CountSubmeshes(ElementItem[] items)
			{
                if (items.Length <= 0)
                    return 0;

                int count = 0;
                var lastId = items[0].matId;
                foreach (var item in items)
				{
                    if (item.matId != lastId)
					{
                        count++;
                        lastId = item.matId;
					}
				}

                return count;
			}
        }

        //Markers / Actions
        Tuple<string, int>[] markers = new Tuple<string, int>[]
        {
            Tuple.Create("$----------------", "skip".GetHashCode()),
            Tuple.Create("[", "newSection".GetHashCode()),
            Tuple.Create("{", "addItemIds".GetHashCode()),
        };

        public int GetAction(string line)
		{
            for (int i = 0; i < markers.Length; i++)
                if (line.StartsWith(markers[i].Item1, StringComparison.Ordinal))
                    return markers[i].Item2;
            return -1;
		}


        Section ProcessSection(string[] lines, SectionDef def)
		{
            Section currectSection = new Section();
            currectSection.Name = def.name;

			for (int currentLine = def.startLine; currentLine < (def.startLine + def.nbLines); currentLine++)
			{
				string line = lines[currentLine];
				int action = GetAction(line);

				if (action == markers[2].Item2)//"addItemIds")
				{
					var l = line.Replace('{', ' ').Replace('}', ' ').Trim();
					currectSection.itemIds = l.Split(' ');
				}
				else
				{
					//Read item
					var item = CreateItem(currectSection);
					item.Read(line, currectSection);
					currectSection.values.Add(item);
				}
			}

			return currectSection;
		}

        public class SectionDef
		{
            public string name;
            public int startLine;
            public int nbLines;

			public SectionDef(string name, int startLine, int nbLines)
			{
                this.name = name;
                this.startLine = startLine;
                this.nbLines = nbLines;
            }
        }

        public Dictionary<string, SectionDef> ExtractSectionDefs(string[] lines)
		{
            Dictionary<string, SectionDef> sections = new Dictionary<string, SectionDef>();

            SectionDef currectSection = null;
            string line = null;
            int currentLine = 0;
            int sectionNbLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                line = lines[i];
                int action = GetAction(line);
                if (action == markers[1].Item2)//"newSection")
                {
                    if (currectSection != null)
                        currectSection.nbLines = sectionNbLines;
                    sectionNbLines = 0;

                    var sectionName = line.Replace('[', ' ').Replace(']', ' ').Trim();
                    currectSection = new SectionDef(sectionName, currentLine+1, sectionNbLines);
                    sections.Add(sectionName, currectSection);
                }
                else if (action != markers[0].Item2)//"skip")
                    sectionNbLines++;

                currentLine++;
            }

            if (currectSection != null)
                currectSection.nbLines = sectionNbLines;

            return sections;
        }

		#endregion

		private Item CreateItem(Section section)
		{
            switch(section.Name)
			{
                case "NODES":
                    return new NodeItem();
                case "ELEMENTS":
                    return new ElementItem();
                case "MATERIALS":
                    return new MaterialItem();
            }

            return new StandardItem();
        }

        public ConcurrentDictionary<string, Section> ParseRDF(AssetImportContext ctx)
        {
            ConcurrentDictionary<string, Section> sections = new ConcurrentDictionary<string, Section>();
            var filestream = File.OpenRead(ctx.assetPath);
            StreamReader reader = new StreamReader(filestream);
            var lines = File.ReadAllLines(ctx.assetPath);

            var defs = ExtractSectionDefs(lines);
            Parallel.ForEach(defs.AsParallel(), sectionDef =>
            {
                var section = ProcessSection(lines, sectionDef.Value);
                sections.TryAdd(section.Name, section);

            });

            return sections;
        }

        private GameObject CreateGameObject(AssetImportContext ctx, Vector3 position, Vector3 scale, string name = "main obj")
		{
            var obj = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
            obj.transform.position = position;
            obj.transform.localScale = scale;

            ctx.AddObjectToAsset(name, obj);
            return obj;
        }

        private Shader GetDefautlShader()
		{
            var rpa = GetCurrentRenderPipeline();
            if (rpa != null && rpa.GetType().ToString().Contains("HighDefinition"))
                return Shader.Find("HDRP/Lit");
            else if (rpa != null)
                return Shader.Find("Universal Render Pipeline/Lit");
            else
                return Shader.Find("Standard");
        }

        public RenderPipelineAsset GetCurrentRenderPipeline()
        {
            return QualitySettings.GetRenderPipelineAssetAt(QualitySettings.GetQualityLevel()) is RenderPipelineAsset qualityAsset ? qualityAsset : GraphicsSettings.renderPipelineAsset;
        }

        private Mesh CreateMesh(AssetImportContext ctx, GameObject go, ConcurrentDictionary<string, Section> sections)
		{
            var filter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();

            filter.mesh = new Mesh();
            filter.sharedMesh.name = go.name;
            filter.sharedMesh.indexFormat = IndexFormat.UInt32;
            var vertices = sections["NODES"].buildVerticeList();
            var indices = sections["ELEMENTS"].buildIndexList();
            var materialSection = sections["MATERIALS"];

            filter.sharedMesh.subMeshCount = materialSection.values.Count;

			var newMaterials = new Material[materialSection.values.Count];
            var defaultShader = GetDefautlShader();

            if (defaultShader != null)
            {
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    var mu = ((MaterialItem)materialSection.values[i]).mu;
                    var colorValue = (mu == 1.0f || mu == 0.0f) ? UnityEngine.Random.Range(0.0f, 1.0f) : mu;
                    var materialName = ((MaterialItem)materialSection.values[i]).name;

                    // Assets must be assigned a unique identifier string consistent across imports
                    var material = new Material(defaultShader);
                    material.color = new Color(colorValue, colorValue, colorValue);
                    material.name = string.IsNullOrEmpty(materialName) ? material.name + i : materialName;
                    ctx.AddObjectToAsset(material.name, material);

                    newMaterials[i] = material;
                }
                meshRenderer.materials = newMaterials;
                meshRenderer.sharedMaterials = newMaterials;
            }

            filter.sharedMesh.SetVertices(vertices);
            foreach (var submesh in indices)
            {
                filter.sharedMesh.SetIndices(submesh.Value.GetRange(0, submesh.Value.Count()), MeshTopology.Triangles, submesh.Key);
            }

            ctx.AddObjectToAsset(filter.sharedMesh.name, filter.sharedMesh);
            return filter.sharedMesh;
        }

        private Collider CreateCollider(AssetImportContext ctx, GameObject go, Mesh mesh)
        {
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            return collider;
        }

        private PhysicMaterial[] CreatePhysicMaterial(AssetImportContext ctx, ConcurrentDictionary<string, Section> sections)
		{
            var materialSection = sections["MATERIALS"];
            var newMaterials = new PhysicMaterial[materialSection.values.Count];
            var defaultShader = GetDefautlShader();

            if (defaultShader != null)
            {
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    var mu = ((MaterialItem)materialSection.values[i]).mu;
                    var materialName = ((MaterialItem)materialSection.values[i]).name;

                    // Assets must be assigned a unique identifier string consistent across imports
                    var material = new PhysicMaterial();
                    material.dynamicFriction = mu;
                    material.name = string.IsNullOrEmpty(materialName) ? material.name + i : materialName;
                    newMaterials[i] = material;
                }
            }

            return newMaterials;
        }

        private Material[] CreateMaterials(AssetImportContext ctx, ConcurrentDictionary<string, Section> sections)
        {
            var materialSection = sections["MATERIALS"];
            var newMaterials = new Material[materialSection.values.Count];
            var defaultShader = GetDefautlShader();

            if (defaultShader != null)
            {
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    var mu = ((MaterialItem)materialSection.values[i]).mu;
                    var colorValue = (mu == 1.0f || mu == 0.0f) ? UnityEngine.Random.Range(0.0f, 1.0f) : mu;
                    var materialName = ((MaterialItem)materialSection.values[i]).name;

                    // Assets must be assigned a unique identifier string consistent across imports
                    var material = new Material(defaultShader);
                    material.color = new Color(colorValue, colorValue, colorValue);
                    material.name = string.IsNullOrEmpty(materialName) ? material.name + i : materialName;
                    newMaterials[i] = material;
                }
            }

            return newMaterials;
        }

        private Mesh[] CreateMeshes(AssetImportContext ctx, ConcurrentDictionary<string, Section> sections)
		{
            var vertices = sections["NODES"].buildVerticeList();
            var indices = sections["ELEMENTS"].buildIndexList();
            var materialSection = sections["MATERIALS"];
            var defaultShader = GetDefautlShader();

            var meshCount = materialSection.values.Count;
            List<Mesh> meshes = new List<Mesh>();
            for(int i = 0; i< meshCount; i++)
			{
                if (!indices.ContainsKey(i))
                    continue;

                var submesh = indices[i];
                var materialName = ((MaterialItem)materialSection.values[i]).name;
                var material = new Material(defaultShader);

                var mesh = new Mesh();
                mesh.name = string.IsNullOrEmpty(materialName) ? material.name + i : materialName;
                mesh.indexFormat = IndexFormat.UInt32;

                mesh.SetVertices(vertices);
                mesh.SetIndices(submesh.GetRange(0, submesh.Count()), MeshTopology.Triangles, 0);

                meshes.Add(mesh);
            }

            return meshes.ToArray();
		}

        private GameObject CreateGameObject(AssetImportContext ctx, Mesh mesh, Material mat, PhysicMaterial pMat, GameObject rootGo = null)
		{
            var go = CreateGameObject(ctx, Vector3.zero, new Vector3(m_Scale, m_Scale, m_Scale), mat.name);
            var filter = go.AddComponent<MeshFilter>();
            filter.name = mat.name;
            filter.sharedMesh = mesh;
            ctx.AddObjectToAsset(filter.sharedMesh.name, filter.sharedMesh);

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.name = mat.name;
            meshRenderer.sharedMaterials = new Material[] { mat };
            ctx.AddObjectToAsset(mat.name, mat);

            //var physicalMaterial = go.AddComponent<PhysicMaterial>()
            var collider = CreateCollider(ctx, go, mesh);
            collider.sharedMaterial = pMat;
            ctx.AddObjectToAsset(pMat.name, pMat);

            if (rootGo != null)
                go.transform.parent = rootGo.transform;

            return go;
		}

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var sections = ParseRDF(ctx);

            if (!m_SplitSubmeshIntoGameObjects)
            {
                var go = CreateGameObject(ctx, Vector3.zero, new Vector3(m_Scale, m_Scale, m_Scale));
                var mesh = CreateMesh(ctx, go, sections);
                var collider = CreateCollider(ctx, go, mesh);
                ctx.SetMainObject(go);
            }
            else
			{
                var materials = CreateMaterials(ctx, sections);
                var physicMaterials = CreatePhysicMaterial(ctx, sections);
                var meshes = CreateMeshes(ctx, sections);

                var rootGo = CreateGameObject(ctx, Vector3.zero, new Vector3(m_Scale, m_Scale, m_Scale), "root");
                var gos = new GameObject[meshes.Length];
                for (int i = 0; i < meshes.Length; i++)
                {
                    gos[i] = CreateGameObject(ctx, meshes[i], materials[i], physicMaterials[i], rootGo);
                }

                ctx.SetMainObject(rootGo);
			}

        }
    }
}
