using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public class WorldGenerator
{
    public enum HeightGeneratorType { Random, Perlin }

    const int sphereLevels = 6;

    PolygonSphere[] spheres;
    IHeightGenerator heightGenerator;
    Action<string> logger = s => Console.WriteLine(s);

    HeightGeneratorType hGenType;
    Noise.Settings heightPerlinSettings;
    float seaLevel;
    int seed;

    public WorldGenerator(Action<string> logger)
    {
        this.logger = logger;
        spheres = new PolygonSphere[sphereLevels];
        for (int i = 0; i < sphereLevels; i++)
        {
            spheres[i] = new PolygonSphere((int)MathF.Pow(2, i) - 1);
        }
    }

    public bool UpdateSettings(
        HeightGeneratorType heightGenType,
        Noise.Settings perlinSettings,
        float seaLevel,
        int seed)
    {
        bool updated = false;

        if (hGenType != heightGenType || heightGenerator == null || this.seed != seed)
        {
            this.seed = seed;
            hGenType = heightGenType;
            updated = true;
            heightPerlinSettings = heightPerlinSettings.ChangeSeed(seed);
            if (heightGenType == HeightGeneratorType.Random)
            {
                heightGenerator = new HeightGenerator(seed);
            }
            else if (heightGenType == HeightGeneratorType.Perlin)
            {
                heightGenerator = new PerlinHeightGenerator(heightPerlinSettings);
            }
        }

        if (!heightPerlinSettings.Equals(perlinSettings))
        {
            heightPerlinSettings = perlinSettings.ChangeSeed(seed);
            if (heightGenType == HeightGeneratorType.Perlin)
            {
                updated = true;
                heightGenerator = new PerlinHeightGenerator(heightPerlinSettings);
            }            
        }

        if (this.seaLevel != seaLevel)
        {
            updated = true;
            this.seaLevel = seaLevel;
        }

        return updated;
    }

    public PolygonSphere GetSphere(int level) => spheres[level];

    public bool RegionIsSea(int sphereLevel, int polygonIndex)
    {
        return spheres[sphereLevel].GetPolygonData(polygonIndex).height < seaLevel;
    }

    public int SphereCount => spheres.Length;

    public void Regenerate()
    {
        for (int i = 0; i < spheres.Length; i++)
        {
            spheres[i].RegenerateData(heightGenerator);
        }
    }

    public int GetPolygonIndex(int polygonIndex, int sphereLevel, int dataLevel)
    {
        for (int level = sphereLevel; level > dataLevel; level--)
        {
            polygonIndex = spheres[level].GetPolygonData(polygonIndex).region;
        }
        return polygonIndex;
    }

    public int GetEdgeIndex(int edgeIndex, int sphereLevel, int dataLevel)
    {
        for (int level = sphereLevel; level > dataLevel && edgeIndex > -1; level--)
        {
            edgeIndex = getRegionEdgeIndex(edgeIndex, level);
        }
        return edgeIndex;
    }

    int getRegionEdgeIndex(int edgeIndex, int sLevel)
    {
        PolygonSphere sphere = spheres[sLevel];
        PolygonSphere parentSphere = spheres[sLevel - 1];
        PolygonData pd1 = sphere.GetPolygonData(sphere.GetEdgePolygon1(edgeIndex));
        PolygonData pd2 = sphere.GetPolygonData(sphere.GetEdgePolygon2(edgeIndex));
        if (pd1.region != pd2.region)
        {
            return parentSphere.GetEdge(pd1.region, pd2.region);
        }
        return -1;
    }
}
