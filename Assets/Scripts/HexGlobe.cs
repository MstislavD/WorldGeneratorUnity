using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexGlobe : MonoBehaviour
{
    enum NormalType { Polyhedron, Sphere }

    public enum Coloring { Zones, Terrain, Random, White }

    static Color[] neighborColors = { Color.red, Color.white, Color.white, Color.cyan, Color.blue, Color.magenta };

    static Color[] typeColors = { Color.red, Color.yellow, Color.green };

    bool initiateMeshing = false;
    bool initiateRecoloring = false;
    Color[] defaultColors, modifiedColors;
    WorldGenerator generator;
    int sphereLevel, dataLevel;
    MeshTopology meshTopology = MeshTopology.Triangles;

    [SerializeField]
    MeshFilter polygonMesh, edgeMesh;

    [SerializeField]
    MeshCollider sphereCollider;

    [SerializeField]
    bool debugSeed = false;

    [SerializeField]
    int seed;

    [SerializeField, Range(0f, 1f)]
    float seaLevel = 0.7f;

    [SerializeField, Range(0f, 1f)]
    float ridgeDensity = 0.1f;

    [SerializeField]
    NormalType normalsType = NormalType.Polyhedron;

    [SerializeField]
    Coloring coloring = Coloring.Zones;

    [SerializeField]
    bool smoothPolygons = true;

    [SerializeField]
    bool smoothEdges = true;

    [SerializeField]
    WorldGenerator.HeightGeneratorType heightGeneratorType = WorldGenerator.HeightGeneratorType.Random;

    [SerializeField]
    Noise.Settings heightPerlinSettings = Noise.Settings.Default;

    private void Awake()
    {
        sphereLevel = 5;
        dataLevel = 5;
        generator = new WorldGenerator(s => Debug.Log(s));
        Regenerate();
    }

    private void Update()
    {
        if (initiateMeshing)
        {
            polygonMesh.mesh = generateSphereMesh();
            sphereCollider.sharedMesh = polygonMesh.mesh;
            initiateMeshing = false;
            initiateRecoloring = true;
        }

        if (initiateRecoloring)
        {
            recolorMesh();
            edgeMesh.mesh = generateEdgeMesh(meshTopology);
            initiateRecoloring = false;
            Debug.Log("Polygons: " + generator.GetSphere(sphereLevel).PolygonCount + ", Seed: " + seed + ", Data Level: " + dataLevel);
        }

        if (Input.GetMouseButtonDown(0))
        {
            PolygonSphere sphere = generator.GetSphere(sphereLevel);
            Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(inputRay, out hit))
            {
                int triagleIndex = hit.triangleIndex;
                int polygonIndex = triagleIndex < 60 ? triagleIndex / 5 : (triagleIndex - 60) / 6 + 12;

                Debug.Log(sphere.GetPolygonInfo(polygonIndex));

                modifiedColors = (Color[])defaultColors.Clone();
                colorPolygonCenter(polygonIndex, modifiedColors, Color.black);

                int count = 0;
                foreach (int neighborIndex in sphere.GetNeighbors(polygonIndex))
                {
                    colorPolygonCenter(neighborIndex, modifiedColors, neighborColors[count++]);
                }

                polygonMesh.mesh.colors = modifiedColors;
            }
        }        
    }

    void colorPolygonCenter(int polygonIndex, Color[] colors, Color color)
    {
        if (polygonIndex > -1)
        {
            int vertexIndex = polygonIndex < 12 ? polygonIndex * 6 : (polygonIndex - 12) * 7 + 72;
            colors[vertexIndex] = color;
        }    
    }

    private void OnValidate()
    {
        if (generator != null)
        {
            bool updated = generator.UpdateSettings(heightGeneratorType, heightPerlinSettings, seaLevel, seed);
            if (updated)
            {
                generator.Regenerate();
                initiateRecoloring = true;
                initiateMeshing = smoothPolygons ? true : initiateMeshing;
            }            
        }
    }

    public void Regenerate()
    {
        seed = debugSeed ? seed : Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(seed);
        bool updated = generator.UpdateSettings(heightGeneratorType, heightPerlinSettings, seaLevel, seed);

        if (updated || !debugSeed)
        {
            generator.Regenerate();
            initiateRecoloring = true;
            initiateMeshing = smoothPolygons ? true : initiateMeshing;
        }        
    }

    public void ChangeSphereLevel(int level)
    {
        if (level != sphereLevel && level < generator.SphereCount)
        {
            sphereLevel = level;
            initiateMeshing = true;
        }
    }

    public void ChangeDataLevel(int step)
    {
        int newLevel = dataLevel + step;
        if (newLevel > -1 && newLevel < generator.SphereCount)
        {
            dataLevel = newLevel;
            initiateRecoloring = true;
            initiateMeshing = smoothPolygons ? true : initiateMeshing;
        }
    }

    public void ResetColors()
    {
        polygonMesh.mesh.colors = modifiedColors = (Color[])defaultColors.Clone();
    }

    public void CycleColoring()
    {
        coloring = coloring + 1;
        if ((int)coloring == 4)
        {
            coloring = 0;
        }
        initiateMeshing = true;
        initiateRecoloring = true;
    }

    Mesh generateSphereMesh()
    {
        PolygonSphere sphere = generator.GetSphere(sphereLevel);

        SphereMeshGeneratorSmoothed.RegionBorderCheck smoothing =
            coloring == Coloring.Random ? regionBorder :
            coloring == Coloring.Terrain ? terrainOrRidgeBorder :
            zoneBorder;
        SphereMeshGenerator meshGenerator = smoothPolygons ?
            new SphereMeshGeneratorSmoothed(sphere, smoothing) : new SphereMeshGenerator(sphere);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();

        for (int i = 0; i < sphere.PolygonCount; i++)
        {
            triangles.AddRange(meshGenerator.GetPolygonTriangles(vertices.Count, sphere.GetSides(i)));
            vertices.AddRange(meshGenerator.GetPolygonVertices(i));
            normals.AddRange(normalsType == NormalType.Polyhedron ? meshGenerator.NormalsFlat(i) : meshGenerator.NormalsSphere(i));
        }

        UnityEngine.Rendering.IndexFormat indexFormat = sphere.PolygonCount < 3000 ?
            UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;

        Mesh mesh = new Mesh { name = "Pentagonal sphere " + sphereLevel, indexFormat = indexFormat };
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();

        return mesh;
    }

    Mesh generateEdgeMesh(MeshTopology topology)
    {
        Color edgeColor = Color.black;

        PolygonSphere sphere = generator.GetSphere(sphereLevel);
        PolygonSphere dataSphere = generator.GetSphere(getDataLevel);

        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        List<Color> colors = new List<Color>();

        SphereMeshGeneratorSmoothed.RegionBorderCheck smoothing = coloring == Coloring.Random ? regionBorder : terrainOrRidgeBorder;
        SphereMeshGenerator meshGenerator = smoothPolygons ?
            new SphereMeshGeneratorSmoothed(sphere, smoothing) : new SphereMeshGenerator(sphere);

        for (int i = 0; i < sphere.EdgeCount; i++)
        {
            int regionEdgeIndex = generator.GetEdgeIndex(i, sphereLevel, dataLevel);
            bool drawEdge = coloring != Coloring.White && coloring != Coloring.Zones && regionEdgeIndex > -1 &&
                (coloring == Coloring.Random ? true : dataSphere.GetEdgeData(regionEdgeIndex).ridge < ridgeDensity);

            if (drawEdge)
            {
                int v0 = vertices.Count;

                vertices.AddRange(
                    topology == MeshTopology.Lines ? meshGenerator.GetEdgeLine(i) :
                    smoothEdges ? meshGenerator.GetEdgeBandSmooth(i) :
                    meshGenerator.GetEdgeBand(i));

                indices.AddRange(
                    topology == MeshTopology.Lines ? new int[] { v0, v0 + 1 } :
                    smoothEdges ? meshGenerator.GetEdgeTrianglesSmooth(v0) :
                    meshGenerator.GetEdgeTriangles(v0));

                colors.AddRange(edgeColor.Populate(vertices.Count - v0));
            }
        }

        UnityEngine.Rendering.IndexFormat indexFormat = vertices.Count < 65000 ?
            UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;

        Mesh mesh = new Mesh() { name = "Pentagonal sphere (edges) " + sphereLevel, indexFormat = indexFormat };
        mesh.vertices = vertices.ToArray();
        mesh.SetIndices(indices, topology, 0);
        mesh.colors = colors.ToArray();

        return mesh;
    }

    bool regionBorder(int p1, int p2)
    {
        return generator.GetPolygonIndex(p1, sphereLevel, dataLevel) != generator.GetPolygonIndex(p2, sphereLevel, dataLevel);
    }

    bool terrainBorder(int p1, int p2)
    {
        int dataLevel = getDataLevel;

        int regionIndex1 = generator.GetPolygonIndex(p1, sphereLevel, dataLevel);
        int regionIndex2 = generator.GetPolygonIndex(p2, sphereLevel, dataLevel);
        bool t1 = generator.RegionIsSea(dataLevel, regionIndex1);
        bool t2 = generator.RegionIsSea(dataLevel, regionIndex2);

        return t1 != t2;
    }

    bool ridgeBorder(int p1, int p2)
    {
        int dataLevel = getDataLevel;
        PolygonSphere sphere = generator.GetSphere(sphereLevel);
        int regionEdgeIndex = generator.GetEdgeIndex(sphere.GetEdge(p1, p2), sphereLevel, dataLevel);
        return regionEdgeIndex > -1 && generator.GetSphere(dataLevel).GetEdgeData(regionEdgeIndex).ridge < ridgeDensity;
    }

    bool zoneBorder(int p1, int p2)
    {
        int dataLevel = getDataLevel;
        var t1 = generator.GetSphere(dataLevel).GetPolygonType(generator.GetPolygonIndex(p1, sphereLevel, dataLevel));
        var t2 = generator.GetSphere(dataLevel).GetPolygonType(generator.GetPolygonIndex(p2, sphereLevel, dataLevel));
        return t1 != t2;
    }

    bool terrainOrRidgeBorder(int p1, int p2)
    {
        return terrainBorder(p1, p2) || ridgeBorder(p1, p2);
    }

    void recolorMesh()
    {
        PolygonSphere sphere = generator.GetSphere(sphereLevel);
        PolygonSphere dataSphere = generator.GetSphere(getDataLevel);

        List<Color> colors = new List<Color>();

        for (int i = 0; i < sphere.PolygonCount; i++)
        {
            int regionIndex = generator.GetPolygonIndex(i, sphereLevel, dataLevel);
            PolygonData data = dataSphere.GetPolygonData(regionIndex);
            var type = dataSphere.GetPolygonType(regionIndex);

            Color color = Color.white;
            if (coloring == Coloring.Random)
            {
                color = data.color;
            }
            else if (coloring == Coloring.Terrain)
            {
                color = generator.RegionIsSea(getDataLevel, regionIndex) ? Color.blue : Color.green;
            }
            else if (coloring == Coloring.Zones)
            {
                color = typeColors[(int)type];
            }

            colors.AddRange(color.Populate(sphere.GetSides(i) + 1));
        }

        defaultColors = colors.ToArray();
        polygonMesh.mesh.colors = modifiedColors = colors.ToArray();
    }

    int getDataLevel => Unity.Mathematics.math.min(dataLevel, sphereLevel);
}
