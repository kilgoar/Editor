﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Experimental.TerrainAPI;
using static WorldConverter;
using static WorldSerialization;

[Serializable]
public class PrefabExport
{
    public int PrefabNumber
    {
        get; set;
    }
    public uint PrefabID
    {
        get; set;
    }
    public string PrefabPath
    {
        get; set;
    }
    public string PrefabPosition
    {
        get; set;
    }
    public string PrefabScale
    {
        get; set;
    }
    public string PrefabRotation
    {
        get; set;
    }
}
public struct TopologyLayers
{
    public float[,,] Topologies
    {
        get; set;
    }
}
public struct GroundTextures
{
    public int Texture
    {
        get; set;
    }
}
public struct BiomeTextures
{
    public int Texture
    {
        get; set;
    }
}
public struct Conditions
{
    public TerrainSplat.Enum GroundConditions
    {
        get; set;
    }
    public TerrainBiome.Enum BiomeConditions
    {
        get; set;
    }
    public TerrainTopology.Enum TopologyLayers
    {
        get; set;
    }
    public bool CheckAlpha
    {
        get; set;
    }
    public int AlphaTexture
    {
        get; set;
    }
    public int TopologyTexture
    {
        get; set;
    }
    public bool CheckHeight
    {
        get; set;
    }
    public float HeightLow
    {
        get; set;
    }
    public float HeightHigh
    {
        get; set;
    }
    public bool CheckSlope
    {
        get; set;
    }
    public float SlopeLow
    {
        get; set;
    }
    public float SlopeHigh
    {
        get; set;
    }
    public int[,,,] AreaRange
    {
        get; set;
    }
}
[ExecuteAlways]
public class MapIO : MonoBehaviour
{
    #region Layers
    public static TerrainTopology.Enum topologyLayerFrom, topologyLayerToPaint, topologyLayer, targetTopologyLayer, conditionalTopology, topologyLayersList, oldTopologyLayer;
    public static TerrainSplat.Enum groundLayerFrom, targetTerrainLayer, groundLayerToPaint, terrainLayer, conditionalGround;
    public static TerrainBiome.Enum biomeLayerFrom, biomeLayerToPaint, biomeLayer, conditionalBiome;
    #endregion
    public static int landSelectIndex = 0;
    public static string landLayer = "Ground", loadPath = "", savePath = "", prefabSavePath = "", bundleFile = "No bundle file selected";
	
	public static TerrainBiome.Enum targetBiomeLayer;
	public static TerrainBiome.Enum paintBiomeLayer;
	
	
    private static PrefabLookup prefabLookup;
    public static float progressBar = 0f, progressValue = 1f;
    public static Dictionary<uint, GameObject> prefabsLoaded = new Dictionary<uint, GameObject>();
    public static Dictionary<string, GameObject> prefabReference = new Dictionary<string, GameObject>();
    public static Texture terrainFilterTexture;
    public static Vector2 heightmapCentre = new Vector2(0.5f, 0.5f);
    private static Terrain terrain;
    #region Editor Input Manager
    [InitializeOnLoadMethod]
    static void EditorInit()
    {
        System.Reflection.FieldInfo info = typeof(EditorApplication).GetField("globalEventHandler", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        EditorApplication.CallbackFunction value = (EditorApplication.CallbackFunction)info.GetValue(null);

        value += EditorGlobalKeyPress;

        info.SetValue(null, value);
    }
    static void EditorGlobalKeyPress()
    {
        //Debug.Log("KEY CHANGE " + Event.current.keyCode);
    }
    #endregion
    public void Start()
    {
        loadPath = "";
        terrainFilterTexture = Resources.Load<Texture>("Textures/Brushes/White128");
        terrain = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
        RefreshAssetList(); // Refreshes the node gen presets.
        GetProjectPrefabs(); // Get all the prefabs saved into the project to a dictionary to reference.
        CentreSceneView(); // Centres the sceneview camera over the middle of the map on project open.
        SetLayers(); // Resets all the layers to default values.
    }
    public static void CentreSceneView()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            sceneView.pivot = new Vector3(500f, 1000f, 500f);
            sceneView.LookAt(new Vector3(500f, 750f, 500f));
        }
    }
    public static void SetLayers()
    {
        topologyLayerFrom = TerrainTopology.Enum.Beach;
        topologyLayerToPaint = TerrainTopology.Enum.Beach;
        groundLayerFrom = TerrainSplat.Enum.Grass;
        groundLayerToPaint = TerrainSplat.Enum.Grass;
        biomeLayerFrom = TerrainBiome.Enum.Temperate;
        biomeLayerToPaint = TerrainBiome.Enum.Temperate;
        topologyLayer = TerrainTopology.Enum.Beach;
        conditionalTopology = (TerrainTopology.Enum)TerrainTopology.NOTHING;
        topologyLayersList = TerrainTopology.Enum.Beach;
        oldTopologyLayer = TerrainTopology.Enum.Beach;
        biomeLayer = TerrainBiome.Enum.Temperate;
        conditionalBiome = (TerrainBiome.Enum)TerrainBiome.NOTHING;
        terrainLayer = TerrainSplat.Enum.Grass;
        conditionalGround = (TerrainSplat.Enum)TerrainSplat.NOTHING;
    }
    /// <summary>
    /// Displays a popup progress bar, the progress is also visible in the taskbar.
    /// </summary>
    /// <param name="title">The Progress Bar title.</param>
    /// <param name="info">The info to be displayed next to the loading bar.</param>
    /// <param name="progress">The progress amount. Between 0f - 1f.</param>
    public static void ProgressBar(string title, string info, float progress)
    {
        EditorUtility.DisplayProgressBar(title, info, progress);
    }
    /// <summary>
    /// Clears the popup progress bar. Needs to be called otherwise it will persist in the editor.
    /// </summary>
    public static void ClearProgressBar()
    {
        MapIO.progressBar = 0;
        EditorUtility.ClearProgressBar();
    }
    public static void SetPrefabLookup(PrefabLookup prefabLookup)
    {
        MapIO.prefabLookup = prefabLookup;
    }
    public static void GetProjectPrefabs()
    {
        prefabsLoaded.Clear();
        foreach (var asset in AssetDatabase.GetAllAssetPaths())
        {
            if (asset.EndsWith(".prefab"))
            {
                GameObject loadedAsset = AssetDatabase.LoadAssetAtPath(asset, typeof(GameObject)) as GameObject;
                if (loadedAsset != null)
                {
                    if (loadedAsset.GetComponent<PrefabDataHolder>() != null)
                    {
                        prefabsLoaded.Add(loadedAsset.GetComponent<PrefabDataHolder>().prefabData.id, loadedAsset);
                    }
                }
            }
        }
    }
    public static PrefabLookup GetPrefabLookUp()
    {
        return prefabLookup;
    }
    /// <summary>
    /// Change the active land layer.
    /// </summary>
    /// <param name="layer">The LandLayer to change to. (Ground, Biome, Alpha & Topology)</param>
    public static void ChangeLayer(string layer)
    {
        landLayer = layer;
        ChangeLandLayer();
    }
    public static void ChangeLandLayer()
    {
        LandData.Save(TerrainTopology.TypeToIndex((int)oldTopologyLayer));
        Undo.ClearAll();
        switch (landLayer.ToLower())
        {
            case "ground":
                LandData.SetLayer("ground");
                break;
            case "biome":
                LandData.SetLayer("biome");
                break;
            case "alpha":
                LandData.SetLayer("alpha");
                break;
            case "topology":
                LandData.SetLayer("topology", TerrainTopology.TypeToIndex((int)topologyLayer));
                break;
        }
    }
    public static GameObject SpawnPrefab(GameObject g, PrefabData prefabData, Transform parent = null)
    {
        Vector3 pos = new Vector3(prefabData.position.x, prefabData.position.y, prefabData.position.z);
        Vector3 scale = new Vector3(prefabData.scale.x, prefabData.scale.y, prefabData.scale.z);
        Quaternion rotation = Quaternion.Euler(new Vector3(prefabData.rotation.x, prefabData.rotation.y, prefabData.rotation.z));
        GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(g);
        newObj.transform.parent = GameObject.FindGameObjectWithTag("Prefabs").transform;
        newObj.transform.position = pos + GetMapOffset();
        newObj.transform.rotation = rotation;
        newObj.transform.localScale = scale;
        newObj.GetComponent<PrefabDataHolder>().prefabData = prefabData;
        return newObj;
    }
    /// <summary>
    /// Removes all the map objects from the scene.
    /// </summary>
    /// <param name="prefabs">Delete Prefab objects.</param>
    /// <param name="paths">Delete Path objects.</param>
    public static void RemoveMapObjects(bool prefabs, bool paths)
    {
        GameObject mapPrefabs = GameObject.Find("Objects");
        if (prefabs)
        {
            foreach (PrefabDataHolder g in mapPrefabs.GetComponentsInChildren<PrefabDataHolder>())
            {
                DestroyImmediate(g.gameObject);
            }
        }
        if (paths)
        {
            foreach (PathDataHolder g in mapPrefabs.GetComponentsInChildren<PathDataHolder>())
            {
                DestroyImmediate(g.gameObject);
            }
        }
    }
    public static Vector3 GetTerrainSize()
    {
        return GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>().terrainData.size;
    }
    public static Vector3 GetMapOffset()
    {
        return 0.5f * GetTerrainSize();
    }
    #region RotateMap Methods
    /// <summary>
    /// Rotates Terrain Map and Water Map 90°.
    /// </summary>
    /// <param name="CW">True = 90°, False = 270°</param>
    public static void RotateHeightmap(bool CW)
    {
        terrain = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
        Terrain water = GameObject.FindGameObjectWithTag("Water").GetComponent<Terrain>();
        float[,] oldHeightMap = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);
        float[,] newHeightMap = new float[terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight];
        float[,] oldWaterMap = water.terrainData.GetHeights(0, 0, water.terrainData.heightmapWidth, water.terrainData.heightmapHeight);
        float[,] newWaterMap = new float[terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight];
        if (CW)
        {
            for (int i = 0; i < oldHeightMap.GetLength(0); i++)
            {
                for (int j = 0; j < oldHeightMap.GetLength(1); j++)
                {
                    newHeightMap[i, j] = oldHeightMap[j, oldHeightMap.GetLength(1) - i - 1];
                    newWaterMap[i, j] = oldWaterMap[j, oldWaterMap.GetLength(1) - i - 1];
                }
            }
        }
        else
        {
            for (int i = 0; i < oldHeightMap.GetLength(0); i++)
            {
                for (int j = 0; j < oldHeightMap.GetLength(1); j++)
                {
                    newHeightMap[i, j] = oldHeightMap[oldHeightMap.GetLength(0) - j - 1, i];
                    newWaterMap[i, j] = oldWaterMap[oldWaterMap.GetLength(0) - j - 1, i];
                }
            }
        }
        terrain.terrainData.SetHeights(0, 0, newHeightMap);
        water.terrainData.SetHeights(0, 0, newWaterMap);
    }
    /// <summary>
    /// Rotates prefabs 90°.
    /// </summary>
    /// <param name="CW">True = 90°, False = 270°</param>
    public static void RotatePrefabs(bool CW)
    {
        var prefabRotate = GameObject.FindGameObjectWithTag("Prefabs");
        if (CW)
        {
            prefabRotate.transform.Rotate(0, 90, 0, Space.World);
        }
        else
        {
            prefabRotate.transform.Rotate(0, -90, 0, Space.World);
        }
    }
    /// <summary>
    /// Rotates paths 90°.
    /// </summary>
    /// <param name="CW">True = 90°, False = 270°</param>
    public static void RotatePaths(bool CW)
    {
        var pathRotate = GameObject.FindGameObjectWithTag("Paths");
        if (CW)
        {
            pathRotate.transform.Rotate(0, 90, 0, Space.World);
        }
        else
        {
            pathRotate.transform.Rotate(0, -90, 0, Space.World);
        }
    }
    /// <summary>
    /// Rotates the selected layer 90°.
    /// </summary>
    /// <param name="landLayer">The LandLayer. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="CW">True = 90°, False = 270°</param>
    /// <param name="topologyLayers">The Topology enums, if selected.</param>
    public static void RotateLayer(string landLayer, bool CW, TerrainTopology.Enum topologyLayers = TerrainTopology.Enum.Beach)
    {
        progressValue = 1f / ReturnSelectedElements(topologyLayers).Count;
        foreach (var topologyInt in ReturnSelectedElements(topologyLayers))
        {
            progressBar += progressValue;
            var rotateText = (landLayer.ToLower() == "topology") ? ((TerrainTopology.Enum)TerrainTopology.IndexToType(topologyInt)).ToString() : landLayer;
            ProgressBar("Rotating Layers", "Rotating: " + rotateText, progressBar);
            int textureCount = TextureCount(landLayer);
            float[,,] oldLayer = GetSplatMap(landLayer, topologyInt);
            float[,,] newLayer = new float[oldLayer.GetLength(0), oldLayer.GetLength(1), textureCount];
            if (CW)
            {
                for (int i = 0; i < newLayer.GetLength(0); i++)
                {
                    for (int j = 0; j < newLayer.GetLength(1); j++)
                    {
                        for (int k = 0; k < textureCount; k++)
                        {
                            newLayer[i, j, k] = oldLayer[j, oldLayer.GetLength(1) - i - 1, k];
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < newLayer.GetLength(0); i++)
                {
                    for (int j = 0; j < newLayer.GetLength(1); j++)
                    {
                        for (int k = 0; k < textureCount; k++)
                        {
                            newLayer[i, j, k] = oldLayer[oldLayer.GetLength(0) - j - 1, i, k];
                        }
                    }
                }
            }
            LandData.SetData(newLayer, landLayer, topologyInt);
        }
        LandData.SetLayer(landLayer, TerrainTopology.TypeToIndex((int)topologyLayer));
        ClearProgressBar();
    }
    /// <summary>
    /// Rotates all Topology layers 90°.
    /// </summary>
    /// <param name="CW">True = 90°, False = 270°</param>
    public static void RotateAllTopologymap(bool CW)
    {
        for (int i = 0; i < TerrainTopology.COUNT; i++)
        {
            RotateLayer("topology", CW, (TerrainTopology.Enum)TerrainTopology.IndexToType(i));
        }
    }
    #endregion
    #region HeightMap Methods
    /// <summary>
    /// Inverts the HeightMap.
    /// </summary>
    public void InvertHeightmap()
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Invert Terrain");
        float[,] landHeightMap = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);
        for (int i = 0; i < landHeightMap.GetLength(0); i++)
        {
            for (int j = 0; j < landHeightMap.GetLength(1); j++)
            {
                landHeightMap[i, j] = 1 - landHeightMap[i, j];
            }
        }
        terrain.terrainData.SetHeights(0, 0, landHeightMap);
    }
    /// <summary>
    /// Normalises the HeightMap between two heights.
    /// </summary>
    /// <param name="normaliseLow">The lowest height the HeightMap should be.</param>
    /// <param name="normaliseHigh">The highest height the HeightMap should be.</param>
    public void NormaliseHeightmap(float normaliseLow, float normaliseHigh)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Normalise Terrain");
        float[,] landHeightMap = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);
        float highestPoint = 0f, lowestPoint = 1f, currentHeight = 0f, heightRange = 0f, normalisedHeightRange = 0f, normalisedHeight = 0f;
        for (int i = 0; i < landHeightMap.GetLength(0); i++)
        {
            for (int j = 0; j < landHeightMap.GetLength(1); j++)
            {
                currentHeight = landHeightMap[i, j];
                if (currentHeight < lowestPoint)
                {
                    lowestPoint = currentHeight;
                }
                else if (currentHeight > highestPoint)
                {
                    highestPoint = currentHeight;
                }
            }
        }
        heightRange = highestPoint - lowestPoint;
        normalisedHeightRange = normaliseHigh - normaliseLow;
        for (int i = 0; i < landHeightMap.GetLength(0); i++)
        {
            for (int j = 0; j < landHeightMap.GetLength(1); j++)
            {
                normalisedHeight = ((landHeightMap[i, j] - lowestPoint) / heightRange) * normalisedHeightRange;
                landHeightMap[i, j] = normaliseLow + normalisedHeight;
            }
        }
        terrain.terrainData.SetHeights(0, 0, landHeightMap);
    }
    /// <summary>
    /// Terraces the HeightMap.
    /// </summary>
    /// <param name="featureSize">The height of each terrace.</param>
    /// <param name="interiorCornerWeight">The weight of the terrace effect.</param>
    public void TerraceErodeHeightmap(float featureSize, float interiorCornerWeight)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Terrace Terrain");
        Material mat = new Material((Shader)AssetDatabase.LoadAssetAtPath("Packages/com.unity.terrain-tools/Shaders/TerraceErosion.shader", typeof(Shader)));
        BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, heightmapCentre, terrain.terrainData.size.x, 0.0f);
        PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds());
        Vector4 brushParams = new Vector4(1.0f, featureSize, interiorCornerWeight, 0.0f);
        mat.SetTexture("_BrushTex", terrainFilterTexture);
        mat.SetVector("_BrushParams", brushParams);
        TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);
        Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, 0);
        TerrainPaintUtility.EndPaintHeightmap(paintContext, "Terrain Filter - TerraceErosion");
    }
    /// <summary>
    /// Smooths the HeightMap.
    /// </summary>
    /// <param name="filterStrength">The strength of the smoothing.</param>
    /// <param name="blurDirection">The direction the smoothing should preference. Between -1f - 1f.</param>
    public void SmoothHeightmap(float filterStrength, float blurDirection)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Smooth Terrain");
        Material mat = TerrainPaintUtility.GetBuiltinPaintMaterial();
        BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, heightmapCentre, terrain.terrainData.size.x, 0.0f);
        PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds());
        Vector4 brushParams = new Vector4(filterStrength, 0.0f, 0.0f, 0.0f);
        mat.SetTexture("_BrushTex", terrainFilterTexture);
        mat.SetVector("_BrushParams", brushParams);
        Vector4 smoothWeights = new Vector4(Mathf.Clamp01(1.0f - Mathf.Abs(blurDirection)), Mathf.Clamp01(-blurDirection), Mathf.Clamp01(blurDirection), 0.0f);
        mat.SetVector("_SmoothWeights", smoothWeights);
        TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, mat);
        Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, mat, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.SmoothHeights);
        TerrainPaintUtility.EndPaintHeightmap(paintContext, "Terrain Filter - Smooth Heights");
    }
    /// <summary>
    /// Sets the edge row of pixels on the HeightMap.
    /// </summary>
    /// <param name="heightToSet">The height to set.</param>
    /// <param name="sides">The sides to set.</param>
    public void SetEdgePixel(float heightToSet, bool[] sides)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Set Edge Pixel");
        float[,] heightMap = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);
        for (int i = 0; i < terrain.terrainData.heightmapHeight; i++)
        {
            for (int j = 0; j < terrain.terrainData.heightmapWidth; j++)
            {
                if (i == 0 && sides[2] == true)
                {
                    heightMap[i, j] = heightToSet / 1000f;
                }
                if (i == terrain.terrainData.heightmapHeight - 1 && sides[0] == true)
                {
                    heightMap[i, j] = heightToSet / 1000f;
                }
                if (j == 0 && sides[3] == true)
                {
                    heightMap[i, j] = heightToSet / 1000f;
                }
                if (j == terrain.terrainData.heightmapWidth - 1 && sides[1] == true)
                {
                    heightMap[i, j] = heightToSet / 1000f;
                }
            }
        }
        terrain.terrainData.SetHeights(0, 0, heightMap);
    }
    /// <summary>
    /// Increases or decreases the HeightMap by the offset.
    /// </summary>
    /// <param name="offset">The amount to offset by. Negative values offset down.</param>
    /// <param name="checkHeight">Check if offsetting the heightmap would exceed the min-max values.</param>
    /// <param name="setWaterMap">Offset the water heightmap.</param>
    public void OffsetHeightmap(float offset, bool checkHeight, bool setWaterMap)
    {
        Terrain water = GameObject.FindGameObjectWithTag("Water").GetComponent<Terrain>();
        float[,] waterMap = water.terrainData.GetHeights(0, 0, water.terrainData.heightmapWidth, water.terrainData.heightmapHeight);
        float[,] heightMap = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);
        offset = offset / 1000f;
        bool heightOutOfRange = false;
        for (int i = 0; i < terrain.terrainData.heightmapHeight; i++)
        {
            for (int j = 0; j < terrain.terrainData.heightmapWidth; j++)
            {
                if (checkHeight == true)
                {
                    if ((heightMap[i, j] + offset > 1f || heightMap[i, j] + offset < 0f) || (waterMap[i, j] + offset > 1f || waterMap[i, j] + offset < 0f))
                    {
                        heightOutOfRange = true;
                        break;
                    }
                    else
                    {
                        heightMap[i, j] += offset;
                        if (setWaterMap == true)
                        {
                            waterMap[i, j] += offset;
                        }
                    }
                }
                else
                {
                    heightMap[i, j] += offset;
                    if (setWaterMap == true)
                    {
                        waterMap[i, j] += offset;
                    }
                }
            }
        }
        if (heightOutOfRange == false)
        {
            terrain.terrainData.SetHeights(0, 0, heightMap);
            water.terrainData.SetHeights(0, 0, waterMap);
        }
        else if (heightOutOfRange == true)
        {
            Debug.Log("Heightmap offset exceeds heightmap limits, try a smaller value.");
        }
    }
    /// <summary>
    /// Sets the water level up to 500 if it's below 500 in height.
    /// </summary>
    public void DebugWaterLevel()
    {
        Terrain water = GameObject.FindGameObjectWithTag("Water").GetComponent<Terrain>();
        float[,] waterMap = water.terrainData.GetHeights(0, 0, water.terrainData.heightmapWidth, water.terrainData.heightmapHeight);
        for (int i = 0; i < waterMap.GetLength(0); i++)
        {
            for (int j = 0; j < waterMap.GetLength(1); j++)
            {
                if (waterMap[i, j] < 0.5f)
                {
                    waterMap[i, j] = 0.5f;
                }
            }
        }
        water.terrainData.SetHeights(0, 0, waterMap);
    }
    /// <summary>
    /// Sets the HeightMap level to the minimum if it's below.
    /// </summary>
    /// <param name="minimumHeight">The minimum height to set.</param>
    public void SetMinimumHeight(float minimumHeight)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Minimum Height Terrain");
        float[,] landMap = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);
        minimumHeight /= 1000f; // Normalise the input to a value between 0 and 1.
        for (int i = 0; i < landMap.GetLength(0); i++)
        {
            for (int j = 0; j < landMap.GetLength(1); j++)
            {
                if (landMap[i, j] < minimumHeight)
                {
                    landMap[i, j] = minimumHeight;
                }
            }
        }
        terrain.terrainData.SetHeights(0, 0, landMap);
    }
    /// <summary>
    /// Puts the heightmap level to the maximum if it's above.
    /// </summary>
    /// <param name="maximumHeight">The maximum height to set.</param>
    public void SetMaximumHeight(float maximumHeight)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Maximum Height Terrain");
        float[,] landMap = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);
        maximumHeight /= 1000f; // Normalise the input to a value between 0 and 1.
        for (int i = 0; i < landMap.GetLength(0); i++)
        {
            for (int j = 0; j < landMap.GetLength(1); j++)
            {
                if (landMap[i, j] > maximumHeight)
                {
                    landMap[i, j] = maximumHeight;
                }
            }
        }
        terrain.terrainData.SetHeights(0, 0, landMap);
    }
    /// <summary>
    /// Returns the height of the HeightMap at the selected coords.
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="z">The Z coordinate.</param>
    /// <returns></returns>
    public float GetHeight(int x, int z)
    {
        float xNorm = (float)x / (float)terrain.terrainData.alphamapHeight;
        float yNorm = (float)z / (float)terrain.terrainData.alphamapHeight;
        float height = terrain.terrainData.GetInterpolatedHeight(xNorm, yNorm);
        return height;
    }
    /// <summary>
    /// Returns a 2D array of the height values.
    /// </summary>
    /// <returns></returns>
    public float[,] GetHeights()
    {
        float alphamapInterp = 1f / terrain.terrainData.alphamapWidth;
        float[,] heights = terrain.terrainData.GetInterpolatedHeights(0, 0, terrain.terrainData.alphamapHeight, terrain.terrainData.alphamapWidth, alphamapInterp, alphamapInterp);
        return heights;
    }
    /// <summary>
    /// Returns the slope of the HeightMap at the selected coords.
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="z">The Z coordinate.</param>
    /// <returns></returns>
    public float GetSlope(int x, int z)
    {
        float xNorm = (float)x / terrain.terrainData.alphamapHeight;
        float yNorm = (float)z / terrain.terrainData.alphamapHeight;
        float slope = terrain.terrainData.GetSteepness(xNorm, yNorm);
        return slope;
    }
    /// <summary>
    /// Returns a 2D array of the slope values.
    /// </summary>
    /// <returns></returns>
    public float[,] GetSlopes()
    {
        float[,] slopes = new float[terrain.terrainData.alphamapHeight, terrain.terrainData.alphamapHeight];
        for (int i = 0; i < terrain.terrainData.alphamapHeight; i++)
        {
            for (int j = 0; j < terrain.terrainData.alphamapHeight; j++)
            {
                float iNorm = (float)i / (float)terrain.terrainData.alphamapHeight;
                float jNorm = (float)j / (float)terrain.terrainData.alphamapHeight;
                slopes[i, j] = terrain.terrainData.GetSteepness(iNorm, jNorm);
            }
        }
        return slopes;
    }
    #endregion
    #region SplatMap Methods
    /// <summary>
    /// Returns the enums selected in the corresponding TerrainLayer enum group.
    /// </summary>
    /// <param name="ground">The TerrainSplat Enum to parse.</param>
    /// <returns></returns>
    public static List<int> ReturnSelectedElements(TerrainSplat.Enum ground)
    {
        List<int> selectedElements = new List<int>();
        for (int i = 0; i < Enum.GetValues(typeof(TerrainSplat.Enum)).Length; i++)
        {
            int layer = 1 << i;
            if (((int)ground & layer) != 0)
            {
                selectedElements.Add(i);
            }
        }
        return selectedElements;
    }
    /// <summary>
    /// Returns the enums selected in the corresponding TerrainLayer enum group.
    /// </summary>
    /// <param name="biome">The TerrainBiome Enum to parse.</param>
    /// <returns></returns>
    public static List<int> ReturnSelectedElements(TerrainBiome.Enum biome)
    {
        List<int> selectedElements = new List<int>();
        for (int i = 0; i < Enum.GetValues(typeof(TerrainBiome.Enum)).Length; i++)
        {
            int layer = 1 << i;
            if (((int)biome & layer) != 0)
            {
                selectedElements.Add(i);
            }
        }
        return selectedElements;
    }
    /// <summary>
    /// Returns the enums selected in the corresponding TerrainLayer enum group.
    /// </summary>
    /// <param name="topology">The TerrainTopology Enum to parse.</param>
    /// <returns></returns>
    public static List<int> ReturnSelectedElements(TerrainTopology.Enum topology)
    {
        List<int> selectedElements = new List<int>();
        for (int i = 0; i < Enum.GetValues(typeof(TerrainTopology.Enum)).Length; i++)
        {
            int layer = 1 << i;
            if (((int)topology & layer) != 0)
            {
                selectedElements.Add(i);
            }
        }
        return selectedElements;
    }
    /// <summary>
    /// Returns the SplatMap at the selected LandLayer.
    /// </summary>
    /// <param name="landLayer">The LandLayer to return. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="topology">The Topology layer, if selected.</param>
    /// <returns></returns>
    public static float[,,] GetSplatMap(string landLayer, int topology = 0)
    {
        switch (landLayer.ToLower())
        {
            default:
                return null;
            case "ground":
                return LandData.groundArray;
            case "biome":
                return LandData.biomeArray;
            case "alpha":
                return LandData.alphaArray;
            case "topology":
                return LandData.topologyArray[topology];
        }
    }
    /// <summary>
    /// Texture count in layer chosen, used for determining the size of the splatmap array.
    /// </summary>
    /// <param name="landLayer">The LandLayer to return the texture count from. (Ground, Biome, Alpha, Topology)</param>
    /// <returns></returns>
    public static int TextureCount(string landLayer)
    {
        if (landLayer.ToLower() == "ground")
        {
            return 8;
        }
        else if (landLayer.ToLower() == "biome")
        {
            return 4;
        }
        return 2;
    }
    /// <summary>
    /// Returns the value of a texture at the selected coords.
    /// </summary>
    /// <param name="landLayer">The LandLayer of the texture. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="texture">The texture to get.</param>
    /// <param name="x">The X coordinate.</param>
    /// <param name="z">The Z coordinate.</param>
    /// <param name="topology">The Topology layer, if selected.</param>
    /// <returns></returns>
    public static float GetTexture(string landLayer, int texture, int x, int z, int topology = 0)
    {
        return GetSplatMap(landLayer, topology)[x, z, texture];
    }
    /// <summary>
    /// Paints if all the conditions passed in are true.
    /// </summary>
    /// <param name="landLayerToPaint">The LandLayer to paint. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="texture">The texture to paint.</param>
    /// <param name="conditions">The conditions to check.</param>
    /// <param name="topology">The Topology layer, if selected.</param>
    public void PaintConditional(string landLayerToPaint, int texture, Conditions conditions, int topology = 0)
    {
        float[,,] groundSplatMap = GetSplatMap("ground");
        float[,,] biomeSplatMap = GetSplatMap("biome");
        float[,,] alphaSplatMap = GetSplatMap("alpha");
        float[,,] topologySplatMap = GetSplatMap("topology", topology);
        float[,,] splatMapPaint = new float[groundSplatMap.GetLength(0), groundSplatMap.GetLength(1), TextureCount(landLayerToPaint)];
        int textureCount = TextureCount(landLayerToPaint);
        float slope, height;
        float[,] heights = new float[terrain.terrainData.alphamapHeight, terrain.terrainData.alphamapHeight];
        float[,] slopes = new float[terrain.terrainData.alphamapHeight, terrain.terrainData.alphamapHeight];
        ProgressBar("Conditional Painter", "Preparing SplatMaps", 0.025f);
        switch (landLayerToPaint.ToLower())
        {
            case "ground":
                splatMapPaint = groundSplatMap;
                break;
            case "biome":
                splatMapPaint = biomeSplatMap;
                break;
            case "alpha":
                splatMapPaint = alphaSplatMap;
                break;
            case "topology":
                splatMapPaint = topologySplatMap;
                break;
        }
        List<TopologyLayers> topologyLayersList = new List<TopologyLayers>();
        List<GroundTextures> groundTexturesList = new List<GroundTextures>();
        List<BiomeTextures> biomeTexturesList = new List<BiomeTextures>();
        ProgressBar("Conditional Painter", "Gathering Conditions", 0.05f);
        foreach (var topologyLayerInt in ReturnSelectedElements(conditions.TopologyLayers))
        {
            topologyLayersList.Add(new TopologyLayers()
            {
                Topologies = GetSplatMap("topology", topologyLayerInt)
            });
        }
        foreach (var groundTextureInt in ReturnSelectedElements(conditions.GroundConditions))
        {
            groundTexturesList.Add(new GroundTextures()
            {
                Texture = groundTextureInt
            });
        }
        foreach (var biomeTextureInt in ReturnSelectedElements(conditions.BiomeConditions))
        {
            biomeTexturesList.Add(new BiomeTextures()
            {
                Texture = biomeTextureInt
            });
        }
        if (conditions.CheckHeight)
        {
            heights = GetHeights();
        }
        if (conditions.CheckSlope)
        {
            slopes = GetSlopes();
        }
        progressValue = 1f / groundSplatMap.GetLength(0);
        for (int i = 0; i < groundSplatMap.GetLength(0); i++)
        {
            progressBar += progressValue;
            ProgressBar("Conditional Painter", "Painting", progressBar);
            for (int j = 0; j < groundSplatMap.GetLength(1); j++)
            {
                if (conditions.CheckSlope)
                {
                    slope = slopes[j, i];
                    if (!(slope >= conditions.SlopeLow && slope <= conditions.SlopeHigh))
                    {
                        continue;
                    }
                }
                if (conditions.CheckHeight)
                {
                    height = heights[i, j];
                    if (!(height >= conditions.HeightLow & height <= conditions.HeightHigh))
                    {
                        continue;
                    }
                }
                foreach (GroundTextures groundTextureCheck in groundTexturesList)
                {
                    if (groundSplatMap[i, j, groundTextureCheck.Texture] < 0.5f)
                    {
                        continue;
                    }
                }
                foreach (BiomeTextures biomeTextureCheck in biomeTexturesList)
                {
                    if (biomeSplatMap[i, j, biomeTextureCheck.Texture] < 0.5f)
                    {
                        continue;
                    }
                }
                if (conditions.CheckAlpha)
                {
                    if (alphaSplatMap[i, j, conditions.AlphaTexture] < 1f)
                    {
                        continue;
                    }
                }
                foreach (TopologyLayers layer in topologyLayersList)
                {
                    if (layer.Topologies[i, j, conditions.TopologyTexture] < 0.5f)
                    {
                        continue;
                    }
                }
                for (int k = 0; k < textureCount; k++)
                {
                    splatMapPaint[i, j, k] = 0;
                }
                splatMapPaint[i, j, texture] = 1f;
            }
        }
        ClearProgressBar();
        groundTexturesList.Clear();
        biomeTexturesList.Clear();
        topologyLayersList.Clear();
        LandData.SetData(splatMapPaint, landLayerToPaint, topology);
        LandData.SetLayer(landLayerToPaint, topology);
    }
    /// <summary>
    /// Paints the layer wherever the height conditions are met. Includes option to blend.
    /// </summary>
    /// <param name="landLayerToPaint">The LandLayer to paint. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="heightLow">The minimum height to paint at 100% weight.</param>
    /// <param name="heightHigh">The maximum height to paint at 100% weight.</param>
    /// <param name="minBlendLow">The minimum height to start to paint. The texture weight will increase as it gets closer to the heightlow.</param>
    /// <param name="maxBlendHigh">The maximum height to start to paint. The texture weight will increase as it gets closer to the heighthigh.</param>
    /// <param name="t">The texture to paint.</param>
    /// <param name="topology">The Topology layer, if selected.</param>
    public void PaintHeight(string landLayerToPaint, float heightLow, float heightHigh, float minBlendLow, float maxBlendHigh, int t, int topology = 0)
    {
        float[,,] splatMap = GetSplatMap(landLayerToPaint, topology);
        int textureCount = TextureCount(landLayerToPaint);
        for (int i = 0; i < splatMap.GetLength(0); i++)
        {
            for (int j = 0; j < (float)splatMap.GetLength(1); j++)
            {
                float iNorm = (float)i / (float)splatMap.GetLength(0);
                float jNorm = (float)j / (float)splatMap.GetLength(1);
                float[] normalised = new float[textureCount];
                float height = terrain.terrainData.GetInterpolatedHeight(jNorm, iNorm); // Normalises the interpolated height to the splatmap size.
                if (height >= heightLow && height <= heightHigh)
                {
                    for (int k = 0; k < textureCount; k++) // Erases the textures on all the layers.
                    {
                        splatMap[i, j, k] = 0;
                    }
                    splatMap[i, j, t] = 1; // Paints the texture t.
                }
                else if (height >= minBlendLow && height <= heightLow)
                {
                    float normalisedHeight = height - minBlendLow;
                    float heightRange = heightLow - minBlendLow;
                    float heightBlend = normalisedHeight / heightRange; // Holds data about the texture weight between the blend ranges.
                    for (int k = 0; k < textureCount; k++)
                    {
                        if (k == t)
                        {
                            splatMap[i, j, t] = heightBlend;
                        }
                        else
                        {
                            splatMap[i, j, k] = splatMap[i, j, k] * Mathf.Clamp01(1f - heightBlend);
                        }
                        normalised[k] = splatMap[i, j, k];
                    }
                    float normalisedWeights = normalised.Sum();
                    for (int k = 0; k < normalised.GetLength(0); k++)
                    {
                        normalised[k] /= normalisedWeights;
                        splatMap[i, j, k] = normalised[k];
                    }
                }
                else if (height >= heightHigh && height <= maxBlendHigh)
                {
                    float normalisedHeight = height - heightHigh;
                    float heightRange = maxBlendHigh - heightHigh;
                    float heightBlendInverted = normalisedHeight / heightRange; // Holds data about the texture weight between the blend ranges.
                    float heightBlend = 1 - heightBlendInverted; // We flip this because we want to find out how close the slope is to the max blend.
                    for (int k = 0; k < textureCount; k++)
                    {
                        if (k == t)
                        {
                            splatMap[i, j, t] = heightBlend;
                        }
                        else
                        {
                            splatMap[i, j, k] = splatMap[i, j, k] * Mathf.Clamp01(1f - heightBlend);
                        }
                        normalised[k] = splatMap[i, j, k];
                    }
                    float normalisedWeights = normalised.Sum();
                    for (int k = 0; k < normalised.GetLength(0); k++)
                    {
                        normalised[k] /= normalisedWeights;
                        splatMap[i, j, k] = normalised[k];
                    }
                }
            }
        }
        LandData.SetData(splatMap, landLayerToPaint, topology);
        LandData.SetLayer(landLayer, topology);
    }
    /// <summary>
    /// Sets whole layer to the active texture. 
    /// </summary>
    /// <param name="landLayerToPaint">The LandLayer to paint. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="t">The texture to paint.</param>
    /// <param name="topology">The Topology layer, if selected.</param>
    public void PaintLayer(string landLayerToPaint, int t, int topology = 0)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData.alphamapTextures, "Paint Layer");
        float[,,] splatMap = GetSplatMap(landLayerToPaint, topology);
        int textureCount = TextureCount(landLayerToPaint);
        for (int i = 0; i < splatMap.GetLength(0); i++)
        {
            for (int j = 0; j < splatMap.GetLength(1); j++)
            {
                for (int k = 0; k < textureCount; k++)
                {
                    splatMap[i, j, k] = 0;
                }
                splatMap[i, j, t] = 1;
            }
        }
        LandData.SetData(splatMap, landLayerToPaint, topology);
        LandData.SetLayer(landLayer, topology);
    }
    /// <summary>
    /// Sets whole layer to the inactive texture. Alpha and Topology only. 
    /// </summary>
    /// <param name="landLayerToPaint">The LandLayer to paint. (Alpha, Topology)</param>
    /// <param name="topologyLayers">The Topology enums, if selected.</param>
    public static void ClearLayer(string landLayerToPaint, TerrainTopology.Enum topologyLayers = TerrainTopology.Enum.Beach)
    {
        progressValue = 1f / ReturnSelectedElements(topologyLayers).Count;
        foreach (var topologyInt in ReturnSelectedElements(topologyLayers))
        {
            progressBar += progressValue;
            ProgressBar("Clearing Layers", "Clearing: " + (TerrainTopology.Enum)TerrainTopology.IndexToType(topologyInt), progressBar);
            float[,,] splatMap = GetSplatMap(landLayerToPaint, topologyInt);
            var alpha = (landLayerToPaint.ToLower() == "alpha") ? true : false;
            for (int i = 0; i < splatMap.GetLength(0); i++)
            {
                for (int j = 0; j < splatMap.GetLength(1); j++)
                {
                    if (alpha)
                    {
                        splatMap[i, j, 0] = 1;
                        splatMap[i, j, 1] = 0;
                    }
                    else
                    {
                        splatMap[i, j, 0] = 0;
                        splatMap[i, j, 1] = 1;
                    }
                }
            }
            LandData.SetData(splatMap, landLayerToPaint, topologyInt);
        }
        LandData.SetLayer(landLayer, TerrainTopology.TypeToIndex((int)topologyLayer));
        ClearProgressBar();
    }
    /// <summary>
    /// Clears all the topology layers.
    /// </summary>
    public static void ClearAllTopologyLayers()
    {
        for (int i = 0; i < TerrainTopology.COUNT; i++)
        {
            ClearLayer("topology", (TerrainTopology.Enum)TerrainTopology.IndexToType(i));
        }
    }
    /// <summary>
    /// Inverts the active and inactive textures. Alpha and Topology only. 
    /// </summary>
    /// <param name="landLayerToPaint">The LandLayer to invert. (Alpha, Topology)</param>
    /// <param name="topologyLayers">The Topology enums, if selected.</param>
    public static void InvertLayer(string landLayerToPaint, TerrainTopology.Enum topologyLayers = TerrainTopology.Enum.Beach)
    {
        progressValue = 1f / ReturnSelectedElements(topologyLayers).Count;
        foreach (var topologyInt in ReturnSelectedElements(topologyLayers))
        {
            progressBar += progressValue;
            ProgressBar("Inverting Layers", "Inverting: " + (TerrainTopology.Enum)TerrainTopology.IndexToType(topologyInt), progressBar);
            float[,,] splatMap = GetSplatMap(landLayerToPaint, topologyInt);
            for (int i = 0; i < splatMap.GetLength(0); i++)
            {
                for (int j = 0; j < splatMap.GetLength(1); j++)
                {
                    if (splatMap[i, j, 0] < 0.5f)
                    {
                        splatMap[i, j, 0] = 1;
                        splatMap[i, j, 1] = 0;
                    }
                    else
                    {
                        splatMap[i, j, 0] = 0;
                        splatMap[i, j, 1] = 1;
                    }
                }
            }
            LandData.SetData(splatMap, landLayerToPaint, topologyInt);
        }
        LandData.SetLayer(landLayer, TerrainTopology.TypeToIndex((int)topologyLayer));
        ClearProgressBar();
    }
    /// <summary>
    /// Inverts all the Topology layers.
    /// </summary>
    public static void InvertAllTopologyLayers()
    {
        for (int i = 0; i < TerrainTopology.COUNT; i++)
        {
            InvertLayer("topology", (TerrainTopology.Enum)TerrainTopology.IndexToType(i));
        }
    }
    /// <summary>
    /// Paints the layer wherever the slope conditions are met. Includes option to blend.
    /// </summary>
    /// <param name="landLayerToPaint">The LandLayer to paint. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="slopeLow">The minimum slope to paint at 100% weight.</param>
    /// <param name="slopeHigh">The maximum slope to paint at 100% weight.</param>
    /// <param name="minBlendLow">The minimum slope to start to paint. The texture weight will increase as it gets closer to the slopeLow.</param>
    /// <param name="maxBlendHigh">The maximum slope to start to paint. The texture weight will increase as it gets closer to the slopeHigh.</param>
    /// <param name="t">The texture to paint.</param>
    /// <param name="topology">The Topology layer, if selected.</param>
    public void PaintSlope(string landLayerToPaint, float slopeLow, float slopeHigh, float minBlendLow, float maxBlendHigh, int t, int topology = 0) // Paints slope based on the current slope input, the slope range is between 0 - 90
    {
        float[,,] splatMap = GetSplatMap(landLayerToPaint, topology);
        int textureCount = TextureCount(landLayerToPaint);
        for (int i = 0; i < splatMap.GetLength(0); i++)
        {
            for (int j = 0; j < splatMap.GetLength(1); j++)
            {
                float iNorm = (float)i / (float)splatMap.GetLength(0);
                float jNorm = (float)j / (float)splatMap.GetLength(1);
                float[] normalised = new float[textureCount];
                float slope = terrain.terrainData.GetSteepness(jNorm, iNorm); // Normalises the steepness coords to match the splatmap array size.
                if (slope >= slopeLow && slope <= slopeHigh)
                {
                    for (int k = 0; k < textureCount; k++)
                    {
                        splatMap[i, j, k] = 0;
                    }
                    splatMap[i, j, t] = 1;
                }
                else if (slope >= minBlendLow && slope <= slopeLow)
                {
                    float normalisedSlope = slope - minBlendLow;
                    float slopeRange = slopeLow - minBlendLow;
                    float slopeBlend = normalisedSlope / slopeRange; // Holds data about the texture weight between the blend ranges.
                    for (int k = 0; k < textureCount; k++) // Gets the weights of the textures in the pos. 
                    {
                        if (k == t)
                        {
                            splatMap[i, j, t] = slopeBlend;
                        }
                        else
                        {
                            splatMap[i, j, k] = splatMap[i, j, k] * Mathf.Clamp01(1f - slopeBlend);
                        }
                        normalised[k] = splatMap[i, j, k];
                    }
                    float normalisedWeights = normalised.Sum();
                    for (int k = 0; k < normalised.GetLength(0); k++)
                    {
                        normalised[k] /= normalisedWeights;
                        splatMap[i, j, k] = normalised[k];
                    }
                }
                else if (slope >= slopeHigh && slope <= maxBlendHigh)
                {
                    float normalisedSlope = slope - slopeHigh;
                    float slopeRange = maxBlendHigh - slopeHigh;
                    float slopeBlendInverted = normalisedSlope / slopeRange; // Holds data about the texture weight between the blend ranges.
                    float slopeBlend = 1 - slopeBlendInverted; // We flip this because we want to find out how close the slope is to the max blend.
                    for (int k = 0; k < textureCount; k++)
                    {
                        if (k == t)
                        {
                            splatMap[i, j, t] = slopeBlend;
                        }
                        else
                        {
                            splatMap[i, j, k] = splatMap[i, j, k] * Mathf.Clamp01(1f - slopeBlend);
                        }
                        normalised[k] = splatMap[i, j, k];
                    }
                    float normalisedWeights = normalised.Sum();
                    for (int k = 0; k < normalised.GetLength(0); k++)
                    {
                        normalised[k] /= normalisedWeights;
                        splatMap[i, j, k] = normalised[k];
                    }
                }
            }
        }
        LandData.SetData(splatMap, landLayerToPaint, topology);
        LandData.SetLayer(landLayer, topology);
    }
	
		public void perlinSaiyan(int l, int p, float s)
	{
	
			
			Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
			float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
			float[,] perlinSum = baseMap;
			
			
			for (int i = 0; i < baseMap.GetLength(0); i++)
			{
					
				for (int j = 0; j < baseMap.GetLength(0); j++)
				{
					perlinSum[i,j] = (0);
				}
			}
			
			
			float r = 0;
			float r1 = 0;
			float amplitude = 1f;
			
			
			for (int u = 1; u <= l; u++)
			{
				
				r = UnityEngine.Random.Range(0,10000)/100f;
				r1 =  UnityEngine.Random.Range(0,10000)/100f;
				amplitude *= .3f;
				
				
				
				for (int i = 0; i < baseMap.GetLength(0); i++)
				{
		
					for (int j = 0; j < baseMap.GetLength(0); j++)
					{
						
						perlinSum[i,j] +=  amplitude * Mathf.PerlinNoise((Mathf.PerlinNoise((Mathf.PerlinNoise(Mathf.PerlinNoise(Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r), Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1)), Mathf.PerlinNoise(Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r), Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1)))), (Mathf.PerlinNoise(Mathf.PerlinNoise(Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r), Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1)), Mathf.PerlinNoise(Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r), Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1)))))),(Mathf.PerlinNoise((Mathf.PerlinNoise(Mathf.PerlinNoise(Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r), Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1)), Mathf.PerlinNoise(Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r), Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1)))), (Mathf.PerlinNoise(Mathf.PerlinNoise(Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r), Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1)), Mathf.PerlinNoise(Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r), Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1)))))));
					}
					EditorUtility.DisplayProgressBar("Generating layer " + u.ToString(), "", (i*1f / baseMap.GetLength(0)*1f));
				}
												
				s = s + p;
				
			}
			EditorUtility.ClearProgressBar();
			for (int i = 0; i < baseMap.GetLength(0); i++)
			{
					
				for (int j = 0; j < baseMap.GetLength(0); j++)
				{
					perlinSum[i,j] = (perlinSum[i,j]/(l)*3f)+.3525f;
				}
			}
			
			
	
			land.terrainData.SetHeights(0, 0, perlinSum);
			//changeLandLayer();
	
	}	
	
	public void diamondSquareNoise(int roughness, int height, int weight)
	{
			Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
			float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
			
			int res = baseMap.GetLength(0);
			float[,] newMap = new float[res,res];
			
			//copied from robert stivanson's 'unity-diamond-square'
			//https://github.com/RobertStivanson
			
			//initialize corners
			
			newMap[0,0] = UnityEngine.Random.Range(475,525)/1000f;
			newMap[res-1,0] = UnityEngine.Random.Range(475,525)/1000f;
			newMap[0,res-1] = UnityEngine.Random.Range(475,525)/1000f;
			newMap[res-1, res-1] = UnityEngine.Random.Range(475,525)/1000f;
			
			
			int j, j2, x, y;
			float avg = 0.5f;
			float range = 1f;			
			
			for (j = res - 1; j > 1; j /= 2) 
			{
				j2 = j / 2;
			
				//diamond
				for (x = 0; x < res - 1; x += j) 
				{
					for (y = 0; y < res - 1; y += j) 
					{
						avg = newMap[x, y];
						avg += newMap[x + j, y];
						avg += newMap[x, y + j];
						avg += newMap[x + j, y + j];
						avg /= 4.0f;

						avg += (UnityEngine.Random.Range(0,height)/1000f - height/1500f) * range;
						newMap[x + j2, y + j2] = avg;
					}
				}
				
				//square
				for (x = 0; x < res - 1; x += j2) 
				{
					for (y = (x + j2) % j; y < res - 1; y += j) 
					{
						avg = newMap[(x - j2 + res - 1) % (res - 1), y];
						avg += newMap[(x + j2) % (res - 1), y];
						avg += newMap[x, (y + j2) % (res - 1)];
						avg += newMap[x, (y - j2 + res - 1) % (res - 1)];
						avg /= 4.0f;

						
						avg += (UnityEngine.Random.Range(0,height)/1000f - height/1500f) * range;
						
						
						newMap[x, y] = avg;

						
						if (x == 0)
						{							
							newMap[res - 1, y] = avg;
						}
						

						if (y == 0) 
						{
							newMap[x, res - 1] = avg;
						}
						
	
					}
				}
				
				range -= (float)(Math.Log10(1+roughness/100f)*range);
			
			
			
			}

			
			for(int h = 0; h < res; h++)
			{
				for(int i = 0; i < res; i++)
				{
					//hi
					newMap[h,i] = (newMap[h,i] * (weight/100f)) + (baseMap[h,i] * (1f-(weight/100f)));
				}
			}
	
			land.terrainData.SetHeights(0, 0, newMap);
	
	}	
	
	public void bluffTerracing(bool flatten, bool perlinBanks, bool circular, float terWeight, int zStart, int gBot, int gTop, int gates, int descaler)
	{
			
			Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
			float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
			float[,] perlinSum = baseMap;
			
			float gateTop = zStart/1000f;
			float gateBottom = .5f;
			float gateRange = 0;
			float gateLoc =0;
			
			/*
			for (int i = 0; i < baseMap.GetLength(0); i++)
				{
						
					for (int j = 0; j < baseMap.GetLength(0); j++)
					{
						baseMap[i,j] = (0);
					}
				}
			
		
				for (int i = 0; i < baseMap.GetLength(0); i++)
				{
						
					for (int j = 0; j < baseMap.GetLength(0); j++)
					{
						perlinSum[i,j] = (0);
					}
				}
			*/
			
				float r = 0;
				float r1 = 0;
				int s = 20;
				

				
				
				
					
					r = UnityEngine.Random.Range(0,10000)/100f;
					r1 =  UnityEngine.Random.Range(0,10000)/100f;
					
					
				/*	
					for (int i = 0; i < baseMap.GetLength(0); i++)
					{
						
						for (int j = 0; j < baseMap.GetLength(0); j++)
						{
							perlinSum[i,j] = perlinSum[i,j] + Mathf.PerlinNoise(i*1f/s+r, j*1f/s+r1);
						}
					}
				*/									
					
					
				
			
			/*			
			for (int i = 0; i < baseMap.GetLength(0); i++)
			{
					
				for (int j = 0; j < baseMap.GetLength(0); j++)
				{
					perlinSum[i,j] = perlinSum[i,j]/l*.5f+.25f;
					

				}
			}
			*/
			for (int g = 0; g < gates; g++)
			{
			//gates
			gateBottom = gateTop*1f;
			gateRange = UnityEngine.Random.Range(gBot,gTop)/1000f;
			gateTop = gateTop + gateRange;
			
			for (int i = 0; i < baseMap.GetLength(0); i++)
			{
					
				for (int j = 0; j < baseMap.GetLength(0); j++)
				{
					
					//flattening
					if (flatten && (baseMap[i,j] <= gateBottom) && g==0)
					{
						baseMap[i,j] = baseMap[i,j] / descaler + gateBottom - (gateBottom/descaler);
					}
					

					/*
					gateLoc = (baseMap[i,j]-gateBottom)/(gateTop-gateBottom);
                                    baseMap[i,j] = baseMap[i,j]-(Mathf.Sin(3.12f * gateLoc) * (gateTop-gateBottom) * terWeight;
					*/
					//gating
					if ((baseMap[i,j] > gateBottom) && (baseMap[i,j] < gateTop))
					{
						//with perlin
						
									gateLoc = (baseMap[i,j]-gateBottom)/(gateTop-gateBottom);
									
									
									
									
									if (circular)
									{
										if (perlinBanks)
										{
											baseMap[i,j] = baseMap[i,j]-(Mathf.Sin(3.12f*gateLoc)* (gateTop-gateBottom)*(perlinSum[i,j] * terWeight* (Mathf.PerlinNoise(i*1f/s+r, j*1f/s+r1)) ));										
										}
										else
										{
											baseMap[i,j] = baseMap[i,j]-(Mathf.Sin(3.12f * gateLoc * .7f) * (gateTop-gateBottom) * terWeight );
										}
									}
									else
									{
										if (perlinBanks)
										{
											baseMap[i,j] = baseMap[i,j]-(gateLoc*.7f) * (gateTop-gateBottom)*(perlinSum[i,j] * terWeight) * (Mathf.PerlinNoise(i*1f/s+r, j*1f/s+r1));										
										}
										else
										{
											baseMap[i,j] = baseMap[i,j]-((gateLoc*.7f) * (gateTop-gateBottom)*terWeight);
										}
									}
					}
						
					//topcropping
					/*
					if ((baseMap[i,j] > gateTop) && (g+1 == gates))
					{
						baseMap[i,j] = gateTop;
					}
					*/
				}
			}
			
			
			
	
		}
	
		land.terrainData.SetHeights(0, 0, baseMap);
	}
	
    /// <summary>
    /// Paints area within these splatmap coords, Maps will always have a splatmap resolution between 512 - 2048 resolution, to the nearest Power of Two (512, 1024, 2048).
    /// Face downright in the editor with Z axis facing up, and X axis facing right, and the map will draw from the bottom left corner, up to the top right. 
    /// Note that the results of how much of the map is covered is dependant on the map size, a 2000 map size would paint almost the bottom half of the map,
    /// whereas a 4000 map would paint up nearly one quarter of the map, and across nearly half of the map.
    /// </summary>
    /// <param name="landLayerToPaint">The LandLayer to paint. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="t">The texture to paint.</param>
    /// <param name="topology">The Topology layer, if selected.</param>
    public void PaintArea(string landLayerToPaint, int z1, int z2, int x1, int x2, int t, int topology = 0)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData.alphamapTextures, "Paint Area");
        float[,,] splatMap = GetSplatMap(landLayerToPaint, topology);
        int textureCount = TextureCount(landLayerToPaint);
        z1 = Mathf.Clamp(z1, 0, splatMap.GetLength(0));
        z2 = Mathf.Clamp(z2, 0, splatMap.GetLength(1));
        x1 = Mathf.Clamp(x1, 0, splatMap.GetLength(0));
        x2 = Mathf.Clamp(x2, 0, splatMap.GetLength(1));
        for (int i = z1; i < z2; i++)
        {
            for (int j = x1; j < x2; j++)
            {
                for (int k = 0; k < textureCount; k++)
                {
                    splatMap[i, j, k] = 0;
                }
                splatMap[i, j, t] = 1;
            }
        }
        LandData.SetData(splatMap, landLayerToPaint, topology);
        LandData.SetLayer(landLayer, topology);
    }
    /// <summary>
    /// Paints the splats wherever the water is above 500 and is above the terrain.
    /// </summary>
    /// <param name="landLayerToPaint">The LandLayer to paint. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="aboveTerrain">Check if the watermap is above the terrain before painting.</param>
    /// <param name="t">The texture to paint.</param>
    /// <param name="topology">The Topology layer, if selected.</param>
    public void PaintRiver(string landLayerToPaint, bool aboveTerrain, int t, int topology = 0)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData.alphamapTextures, "Paint River");
        float[,,] splatMap = GetSplatMap(landLayerToPaint, topology);
        int textureCount = TextureCount(landLayerToPaint);
        Terrain water = GameObject.FindGameObjectWithTag("Water").GetComponent<Terrain>();
        for (int i = 0; i < splatMap.GetLength(0); i++)
        {
            for (int j = 0; j < splatMap.GetLength(1); j++)
            {
                float iNorm = (float)i / (float)splatMap.GetLength(0);
                float jNorm = (float)j / (float)splatMap.GetLength(1);
                float waterHeight = water.terrainData.GetInterpolatedHeight(jNorm, iNorm); // Normalises the interpolated height to the splatmap size.
                float landHeight = terrain.terrainData.GetInterpolatedHeight(jNorm, iNorm); // Normalises the interpolated height to the splatmap size.
                switch (aboveTerrain)
                {
                    case true:
                        if (waterHeight > 500 && waterHeight > landHeight)
                        {
                            for (int k = 0; k < textureCount; k++)
                            {
                                splatMap[i, j, k] = 0;
                            }
                            splatMap[i, j, t] = 1;
                        }
                        break;
                    case false:
                        if (waterHeight > 500)
                        {
                            for (int k = 0; k < textureCount; k++)
                            {
                                splatMap[i, j, k] = 0;
                            }
                            splatMap[i, j, t] = 1;
                        }
                        break;
                }
            }
        }
        LandData.SetData(splatMap, landLayerToPaint, topology);
        LandData.SetLayer(landLayer, topology);
    }
    /// <summary>
    /// Paints a ground texture to the corresponding coordinate if the alpha is active. 
    /// Used for debugging the floating ground clutter that occurs when you have a ground splat of either Grass or Forest ontop of an active alpha layer.
    /// </summary>
    public void AlphaDebug()
    {
        float[,,] splatMap = GetSplatMap("ground");
        float[,,] alphaSplatMap = GetSplatMap("alpha");
        int textureCount = TextureCount("ground");
        for (int i = 0; i < alphaSplatMap.GetLength(0); i++)
        {
            for (int j = 0; j < alphaSplatMap.GetLength(1); j++)
            {
                if (alphaSplatMap[i, j, 1] > 0.9f)
                {
                    for (int k = 0; k < textureCount; k++)
                    {
                        splatMap[i, j, k] = 0;
                    }
                    splatMap[i, j, 3] = 1; // This paints the rock layer. Where 3 = the layer to paint.
                }
            }
        }
        LandData.SetData(splatMap, landLayer);
        LandData.SetLayer(landLayer);
    }
    /// <summary>
    /// Copies the selected texture on a landlayer and paints the same coordinate on another landlayer with the other selected texture.
    /// </summary>
    /// <param name="landLayerFrom">The LandLayer to copy. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="landLayerToPaint">The LandLayer to paint. (Ground, Biome, Alpha, Topology)</param>
    /// <param name="textureFrom">The texture to copy.</param>
    /// <param name="textureToPaint">The texture to paint.</param>
    /// <param name="topologyFrom">The Topology layer to copy from, if selected.</param>
    /// <param name="topologyToPaint">The Topology layer to paint to, if selected.</param>
    public void CopyTexture(string landLayerFrom, string landLayerToPaint, int textureFrom, int textureToPaint, int topologyFrom = 0, int topologyToPaint = 0)
    {
        ProgressBar("Copy Textures", "Copying: " + landLayerFrom, 0.3f);
        float[,,] splatMapFrom = GetSplatMap(landLayerFrom, topologyFrom);
        float[,,] splatMapTo = GetSplatMap(landLayerToPaint, topologyToPaint);
        ProgressBar("Copy Textures", "Pasting: " + landLayerToPaint, 0.5f);
        int textureCount = TextureCount(landLayerToPaint);
        for (int i = 0; i < splatMapFrom.GetLength(0); i++)
        {
            for (int j = 0; j < splatMapFrom.GetLength(1); j++)
            {
                if (splatMapFrom[i, j, textureFrom] > 0)
                {
                    for (int k = 0; k < textureCount; k++)
                    {
                        splatMapTo[i, j, k] = 0;
                    }
                    splatMapTo[i, j, textureToPaint] = 1;
                }
            }
        }
        ProgressBar("Copy Textures", "Pasting: " + landLayerToPaint, 0.9f);
        LandData.SetData(splatMapTo, landLayerToPaint, topologyToPaint);
        LandData.SetLayer(landLayer, topologyToPaint);
        ClearProgressBar();
    }
    #endregion
    /// <summary>
    /// ToDo: Read from a text file instead of having a switch.
    /// </summary>
    public static void RemoveBrokenPrefabs()
    {
        PrefabDataHolder[] prefabs = GameObject.FindObjectsOfType<PrefabDataHolder>();
        Undo.RegisterCompleteObjectUndo(prefabs, "Remove Broken Prefabs");
        var prefabsRemovedCount = 0;
        foreach (PrefabDataHolder p in prefabs)
        {
            switch (p.prefabData.id)
            {
                case 3493139359:
                    DestroyImmediate(p.gameObject);
                    prefabsRemovedCount++;
                    break;
                case 1655878423:
                    DestroyImmediate(p.gameObject);
                    prefabsRemovedCount++;
                    break;
                case 350141265:
                    DestroyImmediate(p.gameObject);
                    prefabsRemovedCount++;
                    break;
            }
        }
        Debug.Log("Removed " + prefabsRemovedCount + " broken prefabs.");
    }
    /// <summary>
    /// Changes all the prefab categories to a the RustEdit custom prefab format. Hide's prefabs from appearing in RustEdit.
    /// </summary>
    public static void HidePrefabsInRustEdit()
    {
        PrefabDataHolder[] prefabDataHolders = GameObject.FindObjectsOfType<PrefabDataHolder>();
        ProgressBar("Hide Prefabs in RustEdit", "Hiding prefabs: ", 0f);
        int prefabsHidden = 0;
        progressValue = 1f / prefabDataHolders.Length;
        for (int i = 0; i < prefabDataHolders.Length; i++)
        {
            progressBar += progressValue;
            ProgressBar("Hide Prefabs in RustEdit", "Hiding prefabs: " + i + " / " + prefabDataHolders.Length, progressBar);
            prefabDataHolders[i].prefabData.category = @":\RustEditHiddenPrefab:" + prefabsHidden + ":";
            prefabsHidden++;
        }
        Debug.Log("Hid " + prefabsHidden + " prefabs.");
        ClearProgressBar();
    }
    /// <summary>
    /// Breaks down RustEdit custom prefabs back into the individual prefabs.
    /// </summary>
    public static void BreakRustEditCustomPrefabs()
    {
        PrefabDataHolder[] prefabDataHolders = GameObject.FindObjectsOfType<PrefabDataHolder>();
        ProgressBar("Break RustEdit Custom Prefabs", "Scanning prefabs", 0f);
        int prefabsBroken = 0;
        progressValue = 1f / prefabDataHolders.Length;
        for (int i = 0; i < prefabDataHolders.Length; i++)
        {
            progressBar += progressValue;
            ProgressBar("Break RustEdit Custom Prefabs", "Scanning prefabs: " + i + " / " + prefabDataHolders.Length, progressBar);
            if (prefabDataHolders[i].prefabData.category.Contains(':'))
            {
                prefabDataHolders[i].prefabData.category = "Decor";
                prefabsBroken++;
            }
        }
        Debug.Log("Broke down " + prefabsBroken + " prefabs.");
        ClearProgressBar();
    }
    /// <summary>
    /// Exports information about all the map prefabs to a JSON file.
    /// </summary>
    /// <param name="mapPrefabFilePath">The JSON file path and name.</param>
    /// <param name="deletePrefabs">Deletes the prefab after the data is exported.</param>
    public static void ExportMapPrefabs(string mapPrefabFilePath, bool deletePrefabs)
    {
        List<PrefabExport> mapPrefabExports = new List<PrefabExport>();
        PrefabDataHolder[] prefabDataHolders = GameObject.FindObjectsOfType<PrefabDataHolder>();
        ProgressBar("Export Map Prefabs", "Exporting...", 0f);
        progressValue = 1f / prefabDataHolders.Length;
        for (int i = 0; i < prefabDataHolders.Length; i++)
        {
            progressBar += progressValue;
            ProgressBar("Export Map Prefabs", "Exporting prefab: " + i + " / " + prefabDataHolders.Length, progressBar);
            mapPrefabExports.Add(new PrefabExport()
            {
                PrefabNumber = i,
                PrefabID = prefabDataHolders[i].prefabData.id,
                PrefabPosition = prefabDataHolders[i].transform.localPosition.ToString(),
                PrefabScale = prefabDataHolders[i].transform.localScale.ToString(),
                PrefabRotation = prefabDataHolders[i].transform.rotation.ToString()
            });
            if (deletePrefabs)
            {
                GameObject.DestroyImmediate(prefabDataHolders[i].gameObject);
            }
        }
        using (StreamWriter streamWriter = new StreamWriter(mapPrefabFilePath, false))
        {
            streamWriter.WriteLine("{");
            foreach (PrefabExport prefabDetail in mapPrefabExports)
            {
                streamWriter.WriteLine("   \"" + prefabDetail.PrefabNumber + "\": \"" + prefabDetail.PrefabID + ":" + prefabDetail.PrefabPosition + ":" + prefabDetail.PrefabScale + ":" + prefabDetail.PrefabRotation + "\",");
            }
            streamWriter.WriteLine("   \"Prefab Count\": " + prefabDataHolders.Length);
            streamWriter.WriteLine("}");
        }
        mapPrefabExports.Clear();
        ClearProgressBar();
        Debug.Log("Exported " + prefabDataHolders.Length + " prefabs.");
    }
    /// <summary>
    /// Exports lootcrates to a JSON for use with Oxide.
    /// </summary>
    /// <param name="prefabFilePath">The path to save the JSON.</param>
    /// <param name="deletePrefabs">Delete the lootcrates after exporting.</param>
    public static void ExportLootCrates(string prefabFilePath, bool deletePrefabs)
    {
        List<PrefabExport> prefabExports = new List<PrefabExport>();
        PrefabDataHolder[] prefabs = GameObject.FindObjectsOfType<PrefabDataHolder>();
        int lootCrateCount = 0;
        foreach (PrefabDataHolder p in prefabs)
        {
            switch (p.prefabData.id)
            {
                case 1603759333:
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_basic.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
                case 3286607235:
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
                case 1071933290:
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_mine.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
                case 2857304752:
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
                case 1546200557:
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
                case 2066926276:
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
                case 1791916628:
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
                case 1892026534:
                    p.transform.Rotate(Vector3.zero, 180f);
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
                case 3852690109:
                    prefabExports.Add(new PrefabExport()
                    {
                        PrefabNumber = lootCrateCount,
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab",
                        PrefabPosition = "(" + p.transform.localPosition.z + ", " + p.transform.localPosition.y + ", " + p.transform.localPosition.x * -1 + ")",
                        PrefabRotation = p.transform.rotation.ToString()
                    });
                    if (deletePrefabs == true)
                    {
                        DestroyImmediate(p.gameObject);
                    }
                    lootCrateCount++;
                    break;
            }
        }
        using (StreamWriter streamWriter = new StreamWriter(prefabFilePath, false))
        {
            streamWriter.WriteLine("{");
            foreach (PrefabExport prefabDetail in prefabExports)
            {
                streamWriter.WriteLine("   \"" + prefabDetail.PrefabNumber + "\": \"" + prefabDetail.PrefabPath + ":" + prefabDetail.PrefabPosition + ":" + prefabDetail.PrefabRotation + "\",");
            }
            streamWriter.WriteLine("   \"Prefab Count\": " + lootCrateCount);
            streamWriter.WriteLine("}");
        }
        prefabExports.Clear();
        Debug.Log("Exported " + lootCrateCount + " lootcrates.");
    }
    private static void LoadMapInfo(MapInfo terrains)
    {
        var worldCentrePrefab = GameObject.FindGameObjectWithTag("Prefabs");
        worldCentrePrefab.transform.position = new Vector3(terrains.size.x / 2, 500, terrains.size.z / 2);
        var worldCentrePath = GameObject.FindGameObjectWithTag("Paths");
        worldCentrePath.transform.position = new Vector3(terrains.size.x / 2, 500, terrains.size.z / 2);
        var worldCentreMapIO = GameObject.FindGameObjectWithTag("MapIO");
        worldCentreMapIO.transform.position = new Vector3(terrains.size.x / 2, 500, terrains.size.z / 2);
        RemoveMapObjects(true, true);
        CentreSceneView();

        var terrainPosition = 0.5f * terrains.size;
        Terrain water = GameObject.FindGameObjectWithTag("Water").GetComponent<Terrain>();
        terrain = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();

        terrain.transform.position = terrainPosition;
        water.transform.position = terrainPosition;

        ProgressBar("Loading: " + loadPath, "Loading Ground Data ", 0.4f);
        TopologyMesh.InitMesh(terrains.topology);

        terrain.terrainData.heightmapResolution = terrains.resolution;
        terrain.terrainData.size = terrains.size;

        water.terrainData.heightmapResolution = terrains.resolution;
        water.terrainData.size = terrains.size;

        terrain.terrainData.SetHeights(0, 0, terrains.land.heights);
        water.terrainData.SetHeights(0, 0, terrains.water.heights);

        terrain.terrainData.alphamapResolution = terrains.resolution - 1;
        terrain.terrainData.baseMapResolution = terrains.resolution - 1;
        water.terrainData.alphamapResolution = terrains.resolution - 1;
        water.terrainData.baseMapResolution = terrains.resolution - 1;

        terrain.GetComponent<UpdateTerrainValues>().setPosition(Vector3.zero);
        water.GetComponent<UpdateTerrainValues>().setPosition(Vector3.zero);

        ProgressBar("Loading: " + loadPath, "Loading Ground Data ", 0.5f);
        LandData.SetData(terrains.splatMap, "ground");

        ProgressBar("Loading: " + loadPath, "Loading Biome Data ", 0.6f);
        LandData.SetData(terrains.biomeMap, "biome");

        ProgressBar("Loading: " + loadPath, "Loading Alpha Data ", 0.7f);
        LandData.SetData(terrains.alphaMap, "alpha");

        ProgressBar("Loading: " + loadPath, "Loading Topology Data ", 0.8f);
        for (int i = 0; i < TerrainTopology.COUNT; i++)
        {
            LandData.SetData(TopologyMesh.getSplatMap(TerrainTopology.IndexToType(i)), "topology", i);
        }
		
        Transform prefabsParent = GameObject.FindGameObjectWithTag("Prefabs").transform;
        GameObject defaultObj = Resources.Load<GameObject>("Prefabs/DefaultPrefab");
        ProgressBar("Loading: " + loadPath, "Spawning Prefabs ", 0.9f);
        float progressValue = 0f;
        for (int i = 0; i < terrains.prefabData.Length; i++)
        {
            progressValue += 0.1f / terrains.prefabData.Length;
            ProgressBar("Loading: " + loadPath, "Spawning Prefabs: " + i + " / " + terrains.prefabData.Length, progressValue + 0.9f);
            if (prefabsLoaded.TryGetValue(terrains.prefabData[i].id, out GameObject newObj))
            {
                newObj = SpawnPrefab(prefabsLoaded[terrains.prefabData[i].id], terrains.prefabData[i], prefabsParent);
            }
            else
            {
                newObj = SpawnPrefab(defaultObj, terrains.prefabData[i], prefabsParent);
            }
        }
        Transform pathsParent = GameObject.FindGameObjectWithTag("Paths").transform;
        GameObject pathObj = Resources.Load<GameObject>("Paths/Path");
        GameObject pathNodeObj = Resources.Load<GameObject>("Paths/PathNode");
        ProgressBar("Loading:" + loadPath, "Spawning Paths ", 0.99f);
        for (int i = 0; i < terrains.pathData.Length; i++)
        {
            Vector3 averageLocation = Vector3.zero;
            for (int j = 0; j < terrains.pathData[i].nodes.Length; j++)
            {
                averageLocation += terrains.pathData[i].nodes[j];
            }
            averageLocation /= terrains.pathData[i].nodes.Length;
            GameObject newObject = Instantiate(pathObj, averageLocation + terrainPosition, Quaternion.identity, pathsParent);

            List<GameObject> pathNodes = new List<GameObject>();
            for (int j = 0; j < terrains.pathData[i].nodes.Length; j++)
            {
                GameObject newNode = Instantiate(pathNodeObj, newObject.transform);
                newNode.transform.position = terrains.pathData[i].nodes[j] + terrainPosition;
                pathNodes.Add(newNode);
            }
            newObject.GetComponent<PathDataHolder>().pathData = terrains.pathData[i];
        }
        LandData.SetLayer(landLayer, TerrainTopology.TypeToIndex((int)topologyLayer)); // Sets the Alphamaps to the layer currently selected.
        ClearProgressBar();
    }
	
	public void pasteMonument(WorldSerialization blob, int x, int y, float zOffset)
	{
		int dim=2000;
		
		EditorUtility.DisplayProgressBar("reeeLoading", "Monument File", .75f);
		//selectedLandLayer = null;
		WorldConverter.MapInfo terrains = WorldConverter.worldToTerrain(blob);
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		var terrainPosition = 0.5f * terrains.size;
		
		float[,] pasteMap = terrains.land.heights;
		float[,] pasteWater = terrains.water.heights;
		float[,,] pSplat = terrains.splatMap;
		float[,,] pBiome = terrains.biomeMap;
		float[,,] pAlpha = terrains.alphaMap;
		//var topos = terrains.topology;
		int res = pSplat.GetLength(0);
		
		TerrainMap<int> pTopoMap = terrains.topology;
		TerrainMap<int> topTerrainMap = TopologyMesh.getTerrainMap();
		
			
		land.transform.position = terrainPosition;
        float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		float ratio = terrains.size.x / (baseMap.GetLength(0));
		
		float x1 = x/2f;
		float y1 = y/2f;
		x=(int)(x/ratio);
		y=(int)(y/ratio);
		
		
		//arrays	
		float[,,] newGround = LandData.groundArray;
		float[,,] newBiome = LandData.biomeArray;
		float[,,] newAlpha = LandData.alphaArray;
		
		EditorUtility.ClearProgressBar();
		
		//find monument dimensions
		//and take a nap or something
		int helk = 0;
		
		for (int i = 0; i < dim; i++)
		{
			for (int j = 0; j < dim; j++)
			{

			if (pBiome[i, j,3] > 0.2f)
				{	
				 dim = j;
				 break;
				}
				
				helk = j;

			}

			if (pBiome[i, helk, 3] > 0.2f)
				{	
				 dim = i;
				 break;
				}
        }
		
		dim = dim + 25;
		
		
		for (int i = 0; i < dim; i++)
		{
			EditorUtility.DisplayProgressBar("Loading", "Monument Layers", (i*1f/dim));
			for (int j = 0; j < dim; j++)
			{

				baseMap[i + x, j + y] = Mathf.Lerp(baseMap[i+x, j+y], pasteMap[i,j]+zOffset, pBiome[i,j,0]);
				
				for (int k = 0; k < 8; k++)
				{
					newGround[i + x, j + y, k] = Mathf.Lerp(newGround[i+x,j+y,k], pSplat[i,j,k], pBiome[i,j,0]);
				}
				
				if (pBiome[i,j,0] > 0.1f)
				{
					topTerrainMap[i + x, j + y] = pTopoMap[i, j];
					
					newAlpha[i + x, j + y, 0] = pAlpha[i, j, 0];
					newAlpha[i + x, j + y, 1] = pAlpha[i, j, 1];
				}
			
				
				
			}
			
			
        }
		
		EditorUtility.ClearProgressBar();
		TopologyMesh.InitMesh(topTerrainMap);
		
		land.terrainData.SetHeights(0,0,baseMap);
		LandData.SetData(newGround, "ground", 0);
		LandData.SetData(newBiome, "biome", 0);
        LandData.SetData(newAlpha, "alpha", 0);
		
		for (int i = 0; i < TerrainTopology.COUNT; i++)
        {
            LandData.SetData(TopologyMesh.getSplatMap(TerrainTopology.IndexToType(i)), "topology", i);
        }
		
		
		
		
		
		Transform prefabsParent = GameObject.FindGameObjectWithTag("Prefabs").transform;
		GameObject defaultObj = Resources.Load<GameObject>("Prefabs/DefaultPrefab");
        
		
		for (int i = 0; i < terrains.prefabData.Length; i++)
        {
			
			terrains.prefabData[i].position.x = terrains.prefabData[i].position.x+y1*2f;
			terrains.prefabData[i].position.z = terrains.prefabData[i].position.z+x1*2f;
			terrains.prefabData[i].position.y = terrains.prefabData[i].position.y + zOffset*1000f;
			
			
			GameObject newObj = SpawnPrefab(defaultObj, terrains.prefabData[i], prefabsParent);
            newObj.GetComponent<PrefabDataHolder>().prefabData = terrains.prefabData[i];
			
			
        }
		
		Transform pathsParent = GameObject.FindGameObjectWithTag("Paths").transform;
        GameObject pathObj = Resources.Load<GameObject>("Paths/Path");
        for (int i = 0; i < terrains.pathData.Length; i++)
        {
            Vector3 averageLocation = Vector3.zero;
            for (int j = 0; j < terrains.pathData[i].nodes.Length; j++)
            {
                averageLocation += terrains.pathData[i].nodes[j];
				terrains.pathData[i].nodes[j].x = terrains.pathData[i].nodes[j].x + y1*2f;
				terrains.pathData[i].nodes[j].z = terrains.pathData[i].nodes[j].z + x1*2f;
				terrains.pathData[i].nodes[j].y = terrains.pathData[i].nodes[j].y + zOffset*1000f;
				
            }
            
			averageLocation /= terrains.pathData[i].nodes.Length;
            
			GameObject newObject = Instantiate(pathObj, averageLocation + terrainPosition, Quaternion.identity, pathsParent);
            newObject.GetComponent<PathDataHolder>().pathData = terrains.pathData[i];
        }
		
		//rip rustedit rustography biggie n tupac
		ChangeLandLayer();
		
	}
	
	public float testMonument(float[,] pasteMap, float[,,] pBiome, float[,] baseMap, int x, int y, int zMin, int zMax)
	{
		int dim=2000;
		x=x/2;
		y=y/2;
		
		
		
		

		
				

		float[,,] newGround = GetSplatMap("ground",0);
		
		landLayer = "topology";
		ChangeLandLayer();
		
		float[,,] monumentMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)topologyLayer));
		
		//find monument dimensions
		int helk = 0;
		
		for (int i = 0; i < dim; i++)
		{
			for (int j = 0; j < dim; j++)
			{

				if (pBiome[i, j,3] > 0.2f)
					{	
					 dim = j;
					 break;
					}
					helk = j;
				}

				if (pBiome[i, helk, 3] > 0.2f)
					{	
					 dim = i;
					 break;
					}
        }

		dim = dim + 25;
		float sum = 0f;
		float sumTarget = 0f;
		int total = 0;
		int monumentSum = 0;
		float heightMax=0f;
		float heightMin=1f;
		float heightDiff=0f;
		float output = 0;
		float diffAvg = 0f;
		float zAvg = 0f;
		
		for (int i = 0; i < dim; i++)
		{
			EditorUtility.DisplayProgressBar("Testing", "Monument location", (i*1f/dim));
			for (int j = 0; j < dim; j++)
			{
				//check just the very edges of the monument for heights and find difference to editor heightmap
				if (pBiome[i, j,0] > 0.3f && pBiome[i, j,0] < .7f)
					{	
						total++;
						sumTarget += pasteMap[i,j]; 
						sum += baseMap[i+x,j+y];
						
						if(pasteMap[i,j] > heightMax)
							heightMax=pasteMap[i,j];
						
						if(pasteMap[i,j] < heightMin)
							heightMin=pasteMap[i,j];
					}
				
				if (monumentMap[i+x,j+y,0] > .3f && pBiome[i,j,0] > 0f)
					{
						monumentSum++;
					}				
			}			
        }
		EditorUtility.ClearProgressBar();
		
		heightDiff = heightMax-heightMin;
		diffAvg = (sum-sumTarget) / (total*1f);
		zAvg = sum / (total*1f);
		
		
		if (monumentSum > 10 || zAvg*1000 < zMin || zAvg*1000 > zMax || heightDiff*1000 > 75)
		{
			output = 9999f;
		}
		else
		{
			output = diffAvg;
		}
		
		return output;
	}
	
	public void placeWaterMonument(WorldSerialization blob, int xMin, int xMax, int yMin, int yMax, int zMin, int zMax)
	{
		EditorUtility.DisplayProgressBar("Loading", "Monument file", .125f);
		WorldConverter.MapInfo terrains = WorldConverter.worldToTerrain(blob);	
		//other arrays
		float[,] pasteMap = terrains.land.heights;
		float[,,] pBiome = terrains.biomeMap;
		
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		
		var terrainPosition = 0.5f * terrains.size;
		land.transform.position = terrainPosition;
		float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		
		EditorUtility.ClearProgressBar();
		
		int x, y;
		int count=0;
		float z;
		
		while(true)
		{
			count++;
			x = UnityEngine.Random.Range(xMin, xMax);
			y = UnityEngine.Random.Range(yMin, yMax);
			
			
			z = testMonument(pasteMap, pBiome, baseMap,x,y,zMin,zMax);
			
			
			if(z != 9999f)
			{
			
			
			z=0f;
			pasteMonument(blob, x, y, z);
			break;
			}
			
			if(count > 20)
			{
				Debug.LogError("Unable to place Monument");
				break;
			}
		}
		
	}
	
	public void placeMonument(WorldSerialization blob, int xMin, int xMax, int yMin, int yMax, int zMin, int zMax)
	{
		EditorUtility.DisplayProgressBar("Loading", "Monument file", .125f);
		
		WorldConverter.MapInfo terrains = WorldConverter.worldToTerrain(blob);
		float[,] pasteMap = terrains.land.heights;
		float[,,] pBiome = terrains.biomeMap;
		
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		
		var terrainPosition = 0.5f * terrains.size;
		land.transform.position = terrainPosition;
		float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		
		EditorUtility.ClearProgressBar();
		
		int x, y;
		int count=0;
		float z;
		
		while(true)
		{
			count++;
			x = UnityEngine.Random.Range(xMin, xMax);
			y = UnityEngine.Random.Range(yMin, yMax);
			
			
			z = testMonument(pasteMap, pBiome, baseMap, x,y,zMin,zMax);
			
			
			if(z != 9999f)
			{
			Debug.LogError(x);
			Debug.LogError(y);
			Debug.LogError(z);
						
			pasteMonument(blob, x, y, z);
			break;
			}
			
			if(count > 20)
			{
				Debug.LogError("Unable to place Monument");
				break;
			}
		}
		
	}
	
	public void stripPrefabsUnderMonument(WorldSerialization blob, int x, int y)
	{
		int dim=2000;
		x=x/2;
		y=y/2;
		
		
		
		WorldConverter.MapInfo terrains = WorldConverter.worldToTerrain(blob);
		
		
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		
		
		
		
		var terrainPosition = 0.5f * terrains.size;
		
		//other arrays
		float[,] pasteMap = terrains.land.heights;
		float[,,] pBiome = terrains.biomeMap;
		
		land.transform.position = terrainPosition;
		
		//heightmap arrays 
		float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		
		
				


		TerrainMap<int> topTerrainMap = TopologyMesh.getTerrainMap();
		TerrainMap<int> pTerrainMap = terrains.topology;
		
		
		float[,,] newGround = LandData.groundArray;
		
		
		//find monument dimensions
		//and take a nap or something
		int helk = 0;
		
		for (int i = 0; i < dim; i++)
		{
			for (int j = 0; j < dim; j++)
			{

				if (pBiome[i, j,3] > 0.2f)
					{	
					 dim = j;
					 break;
					}
					helk = j;
				}

				if (pBiome[i, helk, 3] > 0.2f)
					{	
					 dim = i;
					 break;
					}
        }
		
		
		//GameObject mapPrefabs = GameObject.Find("Objects");

         
			
			
		GameObject prefabs = GameObject.Find("Objects");
		var prefab = prefabs.GetComponentsInChildren<PrefabDataHolder>(true);
		
		
		
		int size = baseMap.GetLength(0);
		float ratio = terrains.size.x / (baseMap.GetLength(0));

		int x1;
		int y1;
		float y5;
		int count = 0;
		
		
		
		for (int k = 0; k < prefab.Length; k++)
		{

					x1 = (int)(prefab[k].gameObject.transform.position.z/2f-x*1f);
					y1 = (int)(prefab[k].gameObject.transform.position.x/2f-y*1f);

					
					if (x1 > 0 && x1 < size && y1 > 0 && y1 < size)
					{

						/*
						if (pBiome[x1,y1,0] > 0 && pBiome[x1,y1,0] <= .8f)
						{
							
							
							prefab[k].snapToGround();
							
						}
						*/
						if (pBiome[x1, y1, 0] > 0.999f)
						{	
							DestroyImmediate(prefab[k].gameObject);
							prefab[k] = null;
							count ++;
						}
						
					}

		}

		
		
		Debug.LogError(count + " prefabs removed");
	}
	
	public void notTopologyLayer()
	{
		oldTopologyLayer = topologyLayer;
		ChangeLandLayer();
		float[,,] splatMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)topologyLayer));
		float[,,] sourceMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)targetTopologyLayer));
		int res = sourceMap.GetLength(0);
		
		for (int m = 0; m < res; m++)
		{
			for (int o = 0; o < res; o++)
			{
				if ((splatMap[m, o, 0] > 0f && sourceMap[m, o, 0] > 0f))
				{
					splatMap[m, o, 0] = float.MinValue;
					splatMap[m, o, 1] = float.MaxValue;
				}
								
			}
		}
		
        LandData.SetData(splatMap, "topology", TerrainTopology.TypeToIndex((int)topologyLayer));
        LandData.SetLayer(landLayer, TerrainTopology.TypeToIndex((int)topologyLayer));
		
	}
	
	public void copyTopologyLayer()
	{
		oldTopologyLayer = topologyLayer;
		ChangeLandLayer();
		float[,,] splatMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)targetTopologyLayer));
		LandData.SetData(splatMap, "topology", TerrainTopology.TypeToIndex((int)topologyLayer));
        LandData.SetLayer("topology", TerrainTopology.TypeToIndex((int)topologyLayer));
	}
	
	public void clearTopology()
	{
		oldTopologyLayer = topologyLayer;
		ChangeLandLayer();
		
		ClearLayer("topology", topologyLayer);
		/*
		float[,,] splatMap = TopologyMesh.getSplatMap((int)topologyLayer);
		int res = splatMap.GetLength(0);
		for (int i = 0; i < res; i++)
			{
				
				
				for (int j = 0; j < res; j++)
				{
					splatMap[i, j, 0] = float.MinValue;
					splatMap[i, j, 1] = float.MaxValue;
				}
			}

		LandData.SetData(splatMap, "topology", TerrainTopology.TypeToIndex((int)topologyLayer));
        LandData.SetLayer("topology", TerrainTopology.TypeToIndex((int)topologyLayer));
		*/
	}
	
	public void invertTopology()
	{
		oldTopologyLayer = topologyLayer;
		ChangeLandLayer();

		InvertLayer("topology", topologyLayer);
	}
	
	public void invertTopologyLayer()
	{
		oldTopologyLayer = topologyLayer;
		ChangeLandLayer();
		
		InvertLayer("topology", topologyLayer);
		ChangeLandLayer();
	}
	
	public void paintTopologyOutline(int w)
	{
			oldTopologyLayer = topologyLayer;
			ChangeLandLayer();

		float[,,] sourceMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)targetTopologyLayer));	
		//float[,,] splatMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)targetTopologyLayer));
		
		int res = sourceMap.GetLength(0);
		float[,,] splatMap = new float[res,res,2];
		float[,,] scratchMap = new float[res,res,2];
		float[,,] hateMap = new float[res,res,2];
		//expand everything n pixels in all directions
		
		for (int i = 1; i < sourceMap.GetLength(0)-1; i++)
			{
				
				for (int j = 1; j < sourceMap.GetLength(1)-1; j++)
				{
					if (sourceMap[i, j, 0] >= .5f)
								{
									scratchMap[i, j, 0] = float.MaxValue;
									scratchMap[i, j, 1] = float.MinValue;
									hateMap[i, j, 0] = float.MaxValue;
									hateMap[i, j, 1] = float.MinValue;
								}
								else
								{
									scratchMap[i, j, 0] = float.MinValue;
									scratchMap[i, j, 1] = float.MaxValue;
									hateMap[i, j, 0] = float.MinValue;
									hateMap[i, j, 1] = float.MaxValue;
								}
				}
			}
		
		
		for (int n = 1; n <= w; n++)
		{
			
			for (int i = 1; i < sourceMap.GetLength(0)-1; i++)
			{
				EditorUtility.DisplayProgressBar("Outlining", TerrainTopology.TypeToIndex((int)targetTopologyLayer).ToString() + " Topology",(i*1f/res));
				for (int j = 1; j < sourceMap.GetLength(1)-1; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						for (int l = -1; l <= 1; l++)
						{
								if (scratchMap[i+k, j+l, 0] >= .5f)
								{
									splatMap[i, j, 0] = float.MaxValue;
									splatMap[i, j, 1] = float.MinValue;
								}
								else
								{
									splatMap[i, j, 0] = float.MinValue;
									splatMap[i, j, 1] = float.MaxValue;
								}
						}					
					}
				}
			}
			
			for (int i = 1; i < sourceMap.GetLength(0)-1; i++)
			{
				for (int j = 1; j < sourceMap.GetLength(1)-1; j++)
				{
					scratchMap[i, j, 0] = splatMap[i, j, 0];
					scratchMap[i, j, 1] = splatMap[i, j, 1];
				}
			}
			EditorUtility.ClearProgressBar();
		}
		
		
		for (int m = 0; m < sourceMap.GetLength(0); m++)
		{
			for (int o = 0; o < sourceMap.GetLength(0); o++)
			{
				if (hateMap[m, o, 0] > 0f  ^ scratchMap[m, o, 0] > 0f)
				{
					splatMap[m, o, 0] = float.MaxValue;
					splatMap[m, o, 1] = float.MinValue;
				}
				else
				{
					splatMap[m, o, 0] = float.MinValue;
					splatMap[m, o, 1] = float.MaxValue;
				}
				
			}
		}
		
        LandData.SetData(splatMap, "topology",  TerrainTopology.TypeToIndex((int)topologyLayer));
        LandData.SetLayer("topology",  TerrainTopology.TypeToIndex((int)topologyLayer));
	}
	
	public void paintHeight(int z1, int z2)
	{
		
		oldTopologyLayer = topologyLayer;
		ChangeLandLayer();
		
		float[,,] splatMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)topologyLayer));
		
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		int res = splatMap.GetLength(0);
		
		for (int i = 0; i < res; i++)
        {
			EditorUtility.DisplayProgressBar("Painting", "Topology",(i*1f/res));
            for (int j = 0; j < res; j++)
            {
                if (baseMap[i, j]*1000f > z1 && baseMap[i,j]*1000f < z2)
				{
					splatMap[i, j, 0] = float.MaxValue;
					splatMap[i, j, 1] = float.MinValue;
				}
				/*
				else
				{
					splatMap[i, j, 0] = float.MinValue;
					splatMap[i, j, 1] = float.MaxValue;
				}
				*/
            }
        }
		EditorUtility.ClearProgressBar();
		LandData.SetData(splatMap, "topology",  TerrainTopology.TypeToIndex((int)topologyLayer));
        LandData.SetLayer("topology",  TerrainTopology.TypeToIndex((int)topologyLayer));
	}
	
	public void eraseHeight(int z1, int z2)
	{
		oldTopologyLayer = topologyLayer;
		ChangeLandLayer();

		float[,,] splatMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)topologyLayer));
		
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		int res = splatMap.GetLength(0);
		
		for (int i = 0; i < res; i++)
        {
			EditorUtility.DisplayProgressBar("Erasing", "Topology",(i*1f/res));
            for (int j = 0; j < res; j++)
            {
                if (baseMap[i, j]*1000f > z1 && baseMap[i,j]*1000f < z2)
				{
					splatMap[i, j, 0] = float.MinValue;
					splatMap[i, j, 1] = float.MaxValue;
				}
            }
        }
		EditorUtility.ClearProgressBar();
		LandData.SetData(splatMap, "topology",  TerrainTopology.TypeToIndex((int)topologyLayer));
        LandData.SetLayer("topology",  TerrainTopology.TypeToIndex((int)topologyLayer));
	}
	
	public void paintPerlin(int s, float c, bool invert, bool paintBiome)
	{
		
		float[,,] newBiome = LandData.biomeArray;
		float[,,] newGround = LandData.groundArray;
		
		int t = TerrainSplat.TypeToIndex((int)terrainLayer);
		int blendhate = 0;
		float o = 0;
		float r = UnityEngine.Random.Range(0,10000)/100f;
		float r1 = UnityEngine.Random.Range(0,10000)/100f;
		int index = TerrainBiome.TypeToIndex((int)targetBiomeLayer);
		float diff = 0f;
		int res = newGround.GetLength(0);
		
		for (int i = 0; i < res; i++)
        {
			EditorUtility.DisplayProgressBar("Gradient Noise", "Textures",(i*1f/res));
            for (int j = 0; j < res; j++)
            {
					o = Mathf.PerlinNoise(i*1f/s+r,j*1f/s+r1);
					
					o *= c;
					
					if (o > 1f)
						o=1f;
					
					if (invert)
						o = 1f - o; 
					
					
					if (paintBiome)
						o *= (newBiome[i,j, index]);
					
										
					
					if (o > 0f)
					{
					newGround[i,j,t] = Math.Max(o, newGround[i,j,t]);
					
					for (int m = 0; m <=7; m++)
									{
										if (m!=t)
											newGround[i,j,m] *= 1f-o;
										
									}
					
					
						
					}						
				
            }
        }
		EditorUtility.ClearProgressBar();
		//dont forget this shit again
		LandData.SetData(newGround, "ground", 0);
		LandData.SetLayer("ground", 0);

	}
	
	public void paintCrazing(int z, int a, int b)
	{
		//z is number of random zones, a is min size, a1 is max
		
		
		//LandData groundLandData = GameObject.FindGameObjectWithTag("Land").transform.Find("Ground").GetComponent<LandData>();
		//	float[,,] newGround = TypeConverter.singleToMulti(groundLandData.splatMap, 8);
		
		//float[,,] newGround = LandData.groundArray;
		
		int t = TerrainSplat.TypeToIndex((int)terrainLayer);
		
		float[,,] newGround = LandData.groundArray;

		
		int s = UnityEngine.Random.Range(a, b);
		int uB = newGround.GetLength(0);
		
		for (int i = 0; i < z; i++)
        {
			EditorUtility.DisplayProgressBar("Painting", "Mottles",(i*1f/z));
			int x = UnityEngine.Random.Range(1, newGround.GetLength(0));
			int y = UnityEngine.Random.Range(1, newGround.GetLength(0));
            for (int j = 0; j < s; j++)
            {
					x = x + UnityEngine.Random.Range(-1,2);
					y = y + UnityEngine.Random.Range(-1,2);

					if (x <= 1)
						x = 2;
					
					if (y <= 1)
						y = 2;
					
					if (x >= uB)
						x = uB-1;
					
					if (y >= uB)
						y = uB-1;
						
					
					newGround[x, y, 0] = 0;
					newGround[x, y, 1] = 0;
					newGround[x, y, 2] = 0;
					newGround[x, y, 3] = 0;
					newGround[x, y, 4] = 0;
					newGround[x, y, 5] = 0;
					newGround[x, y, 6] = 0;
					newGround[x, y, 7] = 0;
					//dirty
					newGround[x, y, t] = 1;								
				
            }
        }
		EditorUtility.ClearProgressBar();
		LandData.SetData(newGround, "ground", 0);
		LandData.SetLayer("ground", 0);
	}
	
	public void paintSplatHeight(int z1, int z2)
	{
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		
		
		float[,,] newGround = LandData.groundArray;
		
		int t = TerrainSplat.TypeToIndex((int)terrainLayer);
		
		
		int res = newGround.GetLength(0);
		for (int i = 0; i < res; i++)
        {
			EditorUtility.DisplayProgressBar("Painting", "Ground",(i*1f/res));
            for (int j = 0; j < res; j++)
            {
                if (baseMap[i, j]*1000f > z1 && baseMap[i,j]*1000f < z2)
				{
					newGround[i, j, 0] = 0;
					newGround[i, j, 1] = 0;
					newGround[i, j, 2] = 0;
					newGround[i, j, 3] = 0;
					newGround[i, j, 4] = 0;
					newGround[i, j, 5] = 0;
					newGround[i, j, 6] = 0;
					newGround[i, j, 7] = 0;
					newGround[i, j, t] = 1;								
				}
            }
        }
		LandData.SetData(newGround, "ground", 0);
		LandData.SetLayer("ground", 0);
	}
	
	public void terrainToTopology(float threshhold)
	{
		oldTopologyLayer = topologyLayer;
		ChangeLandLayer();
		
		float[,,] splatMap = GetSplatMap("topology", TerrainTopology.TypeToIndex((int)topologyLayer));
		float[,,] targetGround = LandData.groundArray;
		int t = TerrainSplat.TypeToIndex((int)targetTerrainLayer);
		
		int res = targetGround.GetLength(0);
		for (int i = 0; i < res; i++)
        {
			EditorUtility.DisplayProgressBar("Copying", "Terrains to Topology",(i*1f/res));
            for (int j = 0; j < res; j++)
            {
                if (targetGround[i,j,t] >= threshhold)
				{
					splatMap[i, j, 0] = float.MaxValue;
					splatMap[i, j, 1] = float.MinValue;
				}
            }
        }
		EditorUtility.ClearProgressBar();
		LandData.SetData(splatMap, "topology",  TerrainTopology.TypeToIndex((int)topologyLayer));
        LandData.SetLayer("topology",  TerrainTopology.TypeToIndex((int)topologyLayer));
	}
	
	
	public void paintTerrainSlope(int s1, int s2)
	{
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		
		
		float[,,] newGround = LandData.groundArray;
		
		int t = TerrainSplat.TypeToIndex((int)terrainLayer);
		float slopeAvg = new float();
		int res = newGround.GetLength(0)-1;
		float[,] slopeMap = new float[res*2,res*2];
		float slope = new float();
		float opacity = 0;
		
		for (int i = 1; i < res; i++)
        {
			EditorUtility.DisplayProgressBar("Painting", "Ground slopes",(i*1f/res));
            for (int j = 1; j < res; j++)
            {

				slope = land.terrainData.GetSteepness(1f*j/newGround.GetLength(0), 1f*i/newGround.GetLength(0));
				
				opacity = 1f;
				
				
				
				if (slope > s1 && slope < s2)
				{
					
					
					newGround[i, j, 0] *= (1-opacity);
					newGround[i, j, 1] *= (1-opacity);
					newGround[i, j, 2] *= (1-opacity);
					newGround[i, j, 3] *= (1-opacity);
					newGround[i, j, 4] *= (1-opacity);
					newGround[i, j, 5] *= (1-opacity);
					newGround[i, j, 6] *= (1-opacity);
					newGround[i, j, 7] *= (1-opacity);
					newGround[i, j, t] = opacity;
				}
					
				
										
					
				
            }
        }
		EditorUtility.ClearProgressBar();
		LandData.SetData(newGround, "ground", 0);
		LandData.SetLayer("ground", 0);
		
	}
	
	public void paintTerrainOutline(int w, float o)
	{
		
		
		
		float[,,] newGround = LandData.groundArray;
		float[,,] outlineGround = LandData.groundArray;
		
		int t = TerrainSplat.TypeToIndex((int)terrainLayer);
		
		int blendhate = 0;
		//mmmmm
		
		int res = newGround.GetLength(0)-1;
		for (int n = 1; n < w+1; n++)
		{
			
			for (int i = 1; i < res; i++)
			{
				EditorUtility.DisplayProgressBar("Outlining", "Pixel " + n.ToString(),(i*1f/res));
				for (int j = 1; j < res; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						for (int l = -1; l <= 1; l++)
						{
								if (newGround[i+k, j+l, t] == 1)
								{
									for (int m = 0; m <=7; m++)
									{
										
										blendhate = 0;
										
										if(m!=t)
										{
											if(newGround[i,j,m] > 0)												
											{
												if (blendhate == 0)
													outlineGround[i, j, m] = 1-o;
												else
													outlineGround[i, j, m] = 0;
												
												blendhate++;
											}
										}
										else
										{
											if(newGround[i, j, t] !=1)
												outlineGround[i, j, t] = o;
										}
									}
								}
						}					
					}
				}
			}
									
		}
		EditorUtility.ClearProgressBar();
		LandData.SetData(newGround, "ground", 0);
		LandData.SetLayer("ground", 0);
	}
	
	public void flattenWater(float z)
	{
		Terrain water = GameObject.FindGameObjectWithTag("Water").GetComponent<Terrain>();
		float[,] baseWater = water.terrainData.GetHeights(0,0, water.terrainData.heightmapWidth, water.terrainData.heightmapHeight);
		for (int i = 0; i < baseWater.GetLength(0); i++)
		{
			for (int j = 0; j < baseWater.GetLength(0); j++)
			{
				baseWater[i,j] = z;
			}
		}
		water.terrainData.SetHeights(0,0,baseWater);
		
	}
	
	
	public void zNudge(float z)
	{
			
			Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
			float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);

				
			
			for (int i = 0; i < baseMap.GetLength(0); i++)
			{
					
				for (int j = 0; j < baseMap.GetLength(0); j++)
				{
					baseMap[i,j] = baseMap[i,j] + (z/1000f);
				}
			}
			
			
						
	
			land.terrainData.SetHeights(0, 0, baseMap);
	}
	
	public void pucker(int radius, int gradient, float seafloor, int xOffset, int yOffset, bool perlin, int s)
	{
		
		//should fix with proper inputs
		
			
				
				float	r = UnityEngine.Random.Range(0,10000)/100f;
				float	r1 =  UnityEngine.Random.Range(0,10000)/100f;
			
			Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
			float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
			
			float[,] perlinShape = new float[baseMap.GetLength(0),baseMap.GetLength(0)];
			
			float[,] puckeredMap = new float[baseMap.GetLength(0),baseMap.GetLength(0)];
			int distance = 0;
			
			Vector2 focusA = new Vector2(baseMap.GetLength(0)/2f+xOffset,baseMap.GetLength(0)/2f+yOffset);
			Vector2 focusB = new Vector2(baseMap.GetLength(0)/2f-xOffset,baseMap.GetLength(0)/2f-yOffset);
			
			
			Vector2 center = new Vector2(baseMap.GetLength(0)/2f,baseMap.GetLength(0)/2f);
			Vector2 scanCord = new Vector2(0f,0f);
			
			int res = baseMap.GetLength(0);
			
				for (int i = 0; i < res; i++)
				{
					EditorUtility.DisplayProgressBar("Puckering", "making island",(i*1f/res));
					for (int j = 0; j < res; j++)
					{
						scanCord.x = i; scanCord.y = j;
						//circular
						//distance = (int)Vector2.Distance(scanCord,center);
						
						distance = (int)Vector2.Distance(scanCord,focusA) + (int)Vector2.Distance(scanCord,focusB);
						
						//if distance from center less than radius, value is 1
						if (distance < radius*2f)
						{
							puckeredMap[i,j] = 1f;
						}
						//otherwise the value should proceed to 0
						else
						{
							puckeredMap[i,j] = 1f-(((distance-radius*2f)*1f) / (gradient*1f));
							
							if (puckeredMap[i,j] < 0)
								puckeredMap[i,j] = 0;
						}
					
					}
				}
												

			
				for (int i = 0; i < res; i++)
				{
					EditorUtility.DisplayProgressBar("Puckering", "cutting channels",(i*1f/res));
					for (int j = 0; j < res; j++)
					{
						
						
						if(perlin)
						{
							perlinShape[i,j] = Mathf.PerlinNoise(i*1f/s+r, j*1f/s+r1)*2f;
							//clamp to 1
							if (perlinShape[i,j] > 1f)
								perlinShape[i,j] = 1f;
							
							baseMap[i,j] = Mathf.Lerp(seafloor, baseMap[i,j], perlinShape[i,j]);
						}
						
						puckeredMap[i,j] = Mathf.Lerp(seafloor, baseMap[i,j], puckeredMap[i,j]);
					}
				}
			
			
			EditorUtility.ClearProgressBar();
			land.terrainData.SetHeights(0, 0, puckeredMap);
	}
	
	
	
	public void punch(float seafloor, int s)
	{
		
		//should fix with proper inputs
		
			
				
				float	r = UnityEngine.Random.Range(0,10000)/100f;
				float	r1 =  UnityEngine.Random.Range(0,10000)/100f;
			
			Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
			float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
			
			float[,] perlinShape = new float[baseMap.GetLength(0),baseMap.GetLength(0)];
			
			
				int res = baseMap.GetLength(0);	

			
				for (int i = 0; i < res; i++)
				{
					EditorUtility.DisplayProgressBar("Punching", "creating island",(i*1f/res));
					for (int j = 0; j < res; j++)
					{
						
						
						
							perlinShape[i,j] = Mathf.PerlinNoise(i*1f/s+r, j*1f/s+r1);
							
							
							baseMap[i,j] = Mathf.Lerp(seafloor, baseMap[i,j], perlinShape[i,j]);
						
						
					}
				}
			
			
			EditorUtility.ClearProgressBar();
			land.terrainData.SetHeights(0, 0, baseMap);
	}
	
	public void biomeGradients(float arcticSize, float tundraSize, float tempSize, float aridSize)
	{
		
		float[,,] newBiome = LandData.biomeArray;
		Vector2 scan;
		
		int size= newBiome.GetLength(0);
		//percentages
		/*
		float arcticSize = 25;
		float tundraSize = 20;
		float tempSize = 30;
		float aridSize = 30;
		*/
		float totalSize = arcticSize + tundraSize + tempSize + aridSize;
		//px transitions
		int transition = 150;
		
		
		arcticSize = arcticSize/totalSize * size;
		tundraSize = tundraSize/totalSize * size + arcticSize;
		tempSize = tempSize/totalSize * size + tundraSize;
		aridSize = aridSize/totalSize * size + tempSize;
		
		float opac;
		int res = newBiome.GetLength(0);
		
		for (int i = 0; i < newBiome.GetLength(0); i++)
					{
						EditorUtility.DisplayProgressBar("Generating", "biomes",(i*1f/res));
						for (int j = 0; j < newBiome.GetLength(0); j++)
						{
							if(i < (arcticSize - transition/2))
							{
									newBiome[j, i, 0] =0;
									newBiome[j, i, 1] =0;
									newBiome[j, i, 2] =0;
									newBiome[j, i, 3] =1;

							}
								if(i >= (arcticSize - transition/2) && i <= (arcticSize + transition/2))
								{
								//gradient	
								opac = (i - arcticSize + transition/2) / transition;
									newBiome[j, i, 0] =0;
									newBiome[j, i, 1] =0;
									newBiome[j, i, 2] =opac;
									newBiome[j, i, 3] =1-opac;
								}
							if(i > (arcticSize + transition/2) && i < (tundraSize - transition/2))
							{
									newBiome[j, i, 0] =0;
									newBiome[j, i, 1] =0;
									newBiome[j, i, 2] =1;
									newBiome[j, i, 3] =0;
							}
								if(i >= (tundraSize - transition/2) && i <= (tundraSize + transition/2))
								{
								//gradient
								opac = (i - tundraSize + transition/2) / transition;
									newBiome[j, i, 0] =0;
									newBiome[j, i, 1] =opac;
									newBiome[j, i, 2] =1-opac;
									newBiome[j, i, 3] =0;								
								}
							if(i > (tundraSize + transition/2) && i < (tempSize - transition/2))
							{
							//clean temperate	
									newBiome[j, i, 0] =0;
									newBiome[j, i, 1] =1;
									newBiome[j, i, 2] =0;
									newBiome[j, i, 3] =0;
							}
								if(i >= (tempSize - transition/2) && i <= (tempSize + transition/2))
								{
								//gradient
								opac = (i - tempSize + transition/2) / transition;
									newBiome[j, i, 0] =opac;
									newBiome[j, i, 1] =1-opac;
									newBiome[j, i, 2] =0;
									newBiome[j, i, 3] =0;	
								}
							if(i > (tempSize + transition/2))
							{
							//clean arid
									newBiome[j, i, 0] =1;
									newBiome[j, i, 1] =0;
									newBiome[j, i, 2] =0;
									newBiome[j, i, 3] =0;
									
							}
							
							
						}
					}
		EditorUtility.ClearProgressBar();
		LandData.SetData(newBiome, "biome", 0);
		LandData.SetLayer(landLayer, 0);			
		
	}
	
	public void insertPrefabCliffs(WorldSerialization blob, Vector3 rotationRanges, int s1, int s2, int k, float zOffset, int density, float floor)
	{
		
				
		
		WorldConverter.MapInfo terrains = WorldConverter.worldToTerrain(blob);
		
		
		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		float[,] baseMap = land.terrainData.GetHeights(0, 0, land.terrainData.heightmapWidth, land.terrainData.heightmapHeight);
		float[,,] monumentMap = LandData.topologyArray[10];
		
		Transform prefabsParent = GameObject.FindGameObjectWithTag("Prefabs").transform;
		GameObject defaultObj = Resources.Load<GameObject>("Prefabs/DefaultPrefab");
        
		int res = baseMap.GetLength(0);
		int size = (int)land.terrainData.size.x;
		int sizeZ = (int)land.terrainData.size.y;
		
		Vector3 position;
		Vector3 rRotate;
		float[] heights = new float[9];
		int p = 0;
		float geology = new float();
		int flipX=0;
		int flipZ=0;
		float slope=0;

		float[,] cliffMap = new float[res,res];
		
		float slopeDiff = 0f;
		float oldPixel = 0f;
		float newPixel = 0f;
		//int[] randomizer = new int[12];
		float randomizer = 0f;
		
		float quotient = 0f;		
		
		float height=0f;
		float tempHeight=0f;
		int xOff=0;
		int yOff=0;
		/*
		
		wikidpedia pseudocode:
		
		oldpixel  := pixel[x][y]
      newpixel  := find_closest_palette_color(oldpixel)
      pixel[x][y]  := newpixel
      quant_error  := oldpixel - newpixel
      pixel[x + 1][y    ] := pixel[x + 1][y    ] + quant_error * 7 / 16
      pixel[x - 1][y + 1] := pixel[x - 1][y + 1] + quant_error * 3 / 16
      pixel[x    ][y + 1] := pixel[x    ][y + 1] + quant_error * 5 / 16
      pixel[x + 1][y + 1] := pixel[x + 1][y + 1] + quant_error * 1 / 16
		*/
		
		
		for (int i = 0; i < res; i++)
        {
			EditorUtility.DisplayProgressBar("Generating", "Slope Map",(i*1f/res));
            for (int j = 0; j < res; j++)
            {
				
				cliffMap[i,j] = (land.terrainData.GetSteepness(1f*j/res, 1f*i/res))/90 * (density / 100f);
				
				//cliffMap[i,j] = i*1f/res/2f + j*1f/res/2f;
				
			}
		}
		EditorUtility.ClearProgressBar();
		/*
		quotient=0;
				//randomizer = 1f;
				for (int f = 0; f < 12; f++)
				{
					randomizer[f] = Random.Range(0, 12);
					quotient+=randomizer[f];
					Debug.LogError(randomizer[f]);
				}
				Debug.LogError(quotient);
		*/
		
		for (int i = 2; i < res-2; i++)
        {
			EditorUtility.DisplayProgressBar("Dithering", "Cliff Map",(i*1f/res));
            for (int j = 2; j < res-2; j++)
            {
				
				oldPixel = cliffMap[i,j];
				
				if (cliffMap[i,j] >= .5f)
					{
						newPixel = 1f;
					}
				else
					{
						newPixel = 0f;
					}
					
				cliffMap[i,j] = newPixel;
				
				slopeDiff = (oldPixel-newPixel);
				
				/*
				cliffMap[i+1,j] = cliffMap[i+1,j] + (slopeDiff * 7f/16f);
				cliffMap[i-1,j-1] = cliffMap[i-1,j-1] + (slopeDiff * 3f/16f);
				cliffMap[i,j+1] = cliffMap[i,j+1] + (slopeDiff * 5f/16f);
				cliffMap[i+1,j+1] = cliffMap[i+1,j+1] + (slopeDiff * 1f/16f);
				
				//sierra fast
				*/
				/*
				randomizer = Random.Range(.95f,1.1f);
				
				cliffMap[i+1,j] = cliffMap[i+1,j] + slopeDiff * (2f/4f*randomizer);
				cliffMap[i-1,j-1] = cliffMap[i-1,j-1] + slopeDiff * (1f/4f*randomizer/2f);
				cliffMap[i,j+1] = cliffMap[i,j+1] + slopeDiff * (1f/4f*randomizer/2f);
				*/
				
				//   *xy
				// yxxxz
				// zzyyz
				
				//  *5
				// 252
				
				// /14
				
				randomizer = UnityEngine.Random.Range(0f,1f);
				quotient = 42f;
				
				cliffMap[i+1,j] = cliffMap[i+1,j] + (slopeDiff * 8f * randomizer / quotient);
				cliffMap[i-1,j+1] = cliffMap[i-1,j+1] + (slopeDiff * 4f * randomizer /quotient);
				cliffMap[i,j+1] = cliffMap[i,j+1] + (slopeDiff * 8f * randomizer / quotient);
				cliffMap[i+1,j+1] = cliffMap[i+1,j+1] + (slopeDiff * 4f * randomizer / quotient);

				randomizer = UnityEngine.Random.Range(1f,1.5f);
				
				cliffMap[i+2,j] = cliffMap[i+2,j] + (slopeDiff * 4f * randomizer / quotient);
				cliffMap[i-2,j+1] = cliffMap[i-2,j+1] + (slopeDiff * 2f * randomizer / quotient);
				cliffMap[i,j+2] = cliffMap[i,j+2] + (slopeDiff * 4f * randomizer / quotient);
				cliffMap[i+2,j+1] = cliffMap[i+2,j+1] + (slopeDiff * 2f * randomizer / quotient);
				
				randomizer = UnityEngine.Random.Range(1f,2f);
				
				cliffMap[i+1,j+2] = cliffMap[i+1,j+2] + (slopeDiff * 2f * randomizer / quotient);
				cliffMap[i-2,j+2] = cliffMap[i-2,j+2] + (slopeDiff * 1f * randomizer /quotient);
				cliffMap[i-1,j+2] = cliffMap[i-1,j+2] + (slopeDiff * 2f * randomizer / quotient);
				cliffMap[i+2,j+2] = cliffMap[i+2,j+2] + (slopeDiff * 1f * randomizer / quotient);
				
				
				/*
				randomizer = Random.Range(0f,2f);
				cliffMap[i+1,j] = cliffMap[i+1,j] + (slopeDiff * randomizer * 7f/16f);
				cliffMap[i-1,j-1] = cliffMap[i-1,j-1] + (slopeDiff * randomizer * 3f/16f);
				cliffMap[i,j+1] = cliffMap[i,j+1] + (slopeDiff * randomizer) * 5f/16f;
				cliffMap[i+1,j+1] = cliffMap[i+1,j+1] + (slopeDiff * randomizer)* 1f/16f;
				*/
				
			}
		}
		EditorUtility.ClearProgressBar();
		for (int i = 1; i < res-1; i++)
        {
			EditorUtility.DisplayProgressBar("Spawning", "Geology features",(i*1f/res));
            for (int j = 1; j < res-1; j++)
            {
				slope = land.terrainData.GetSteepness(1f*j/res, 1f*i/res);
				
						
				
				if(baseMap[i,j] > floor/1000f && monumentMap[i,j,0] <.01f && cliffMap[i,j]>=.5f && slope > s1 && slope < s2)
				{
					//for debugging
							//monumentMap[i,j,0] = 1f;
							//monumentMap[i,j,1] = 0f;
							
									//all nextdoor pixel heights
									p=0;
									height = 0f;
									tempHeight= 0f;
									for (int n = -1; n < 2; n++)
									{
										for (int o = -1; o < 2; o++)
										{
											tempHeight = baseMap[i+n, j+o];
											
											if (height < tempHeight)
											{
												height = tempHeight;
												xOff = n;
												yOff = o;
											}
											
											
											
										}
									}
									
									
									GameObject newObj;
																		
									//chance of flipping 180 on x or z for 'variety'
									flipX = UnityEngine.Random.Range(0,2) * 180;
									flipZ = UnityEngine.Random.Range(0,2) * 180;
									
									//lean and displace each rock for 'geology'
									geology = (Mathf.PerlinNoise(i*1f/80,j*1f/80))*20;
									
									//position is nearest highest pixel + zoffset
									position = new Vector3(j *2f-(size*.5f)+yOff, height * sizeZ - (sizeZ*.5f) + zOffset,i *2f-(size*.5f)+xOff);
									
									//rotation gets geology and randomization
									rRotate = new Vector3(UnityEngine.Random.Range(0, rotationRanges.x) + geology + flipX, UnityEngine.Random.Range(0, rotationRanges.y), UnityEngine.Random.Range(0,rotationRanges.z) + flipZ);
									
									//spawn object from cliffset with index k
									newObj = reeSpawnPrefab(position, rRotate, defaultObj, terrains.prefabData[k], prefabsParent);
									newObj.GetComponent<PrefabDataHolder>().prefabData.id = terrains.prefabData[k].id;
									newObj.GetComponent<PrefabDataHolder>().prefabData.category = terrains.prefabData[k].category;
									newObj.GetComponent<PrefabDataHolder>().prefabData.scale = terrains.prefabData[k].scale;
							
				}
				
				
            }
			
        }
		EditorUtility.ClearProgressBar();
	}
	
	public GameObject reeSpawnPrefab(Vector3 pos, Vector3 rot, GameObject g, PrefabData prefabData, Transform parent = null)
    {
       
        Vector3 scale = new Vector3(prefabData.scale.x, prefabData.scale.y, prefabData.scale.z);
        
		Quaternion rotation = new Quaternion();
		rotation.eulerAngles = (rot+prefabData.rotation);
		
        GameObject newObj = Instantiate(g, pos + GetMapOffset(), rotation, parent);
        newObj.transform.localScale = scale;

        return newObj;
    }
	
    public static void Load(WorldSerialization blob)
    {
        WorldConverter.MapInfo terrains = WorldConverter.worldToTerrain(blob);
        MapIO.ProgressBar("Loading: " + loadPath, "Loading Land Heightmap Data ", 0.3f);
        LoadMapInfo(terrains);
    }
    /// <summary>
    /// Saves the map.
    /// </summary>
    /// <param name="path">The path to save to.</param>
    public void Save(string path)
    {
        LandData.Save(TerrainTopology.TypeToIndex((int)topologyLayer));
        foreach (var item in GameObject.FindGameObjectWithTag("World").GetComponentsInChildren<Transform>(true))
        {
            item.gameObject.SetActive(true);
        }
        Terrain terrain = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
        Terrain water = GameObject.FindGameObjectWithTag("Water").GetComponent<Terrain>();
        ProgressBar("Saving Map: " + savePath, "Saving Watermap ", 0.25f);
        ProgressBar("Saving Map: " + savePath, "Saving Prefabs ", 0.4f);
        WorldSerialization world = WorldConverter.terrainToWorld(terrain, water);
        ProgressBar("Saving Map: " + savePath, "Saving Layers ", 0.6f);
        world.Save(path);
        ProgressBar("Saving Map: " + savePath, "Saving to disk ", 0.8f);
        ClearProgressBar();
    }
    /// <summary>
    /// Creates a new flat terrain.
    /// </summary>
    /// <param name="size">The size of the terrain.</param>
    public void NewEmptyTerrain(int size)
    {
        LoadMapInfo(WorldConverter.emptyWorld(size));
        ClearAllTopologyLayers();
        ClearLayer("Alpha");
        PaintLayer("Biome", 1);
        PaintLayer("Ground", 4);
        SetMinimumHeight(501f);
    }
    public void StartPrefabLookup()
    {
        SetPrefabLookup(new PrefabLookup(bundleFile, this));
    }
    /// <summary>
    /// Clears the topologies only used by the server. 
    /// </summary>
    public static void ClearServerSideTopologies()
    {
        for (int i = 0; i < TerrainTopology.COUNT; i++)
        {
            if (i != 14 && i != 16) // Checks for River and Lake.
            {
                ClearLayer("topology", (TerrainTopology.Enum)TerrainTopology.IndexToType(i));
            }
        }
    }
    public static List<string> generationPresetList = new List<string>();
    public static Dictionary<string, UnityEngine.Object> nodePresetLookup = new Dictionary<string, UnityEngine.Object>();
    /// <summary>
    /// Refreshes and adds the new NodePresets in the generationPresetList.
    /// </summary>
    public static void RefreshAssetList()
    {
        var list = AssetDatabase.FindAssets("t:AutoGenerationGraph");
        generationPresetList.Clear();
        nodePresetLookup.Clear();
        foreach (var item in list)
        {
            var itemName = AssetDatabase.GUIDToAssetPath(item).Split('/');
            var itemNameSplit = itemName[itemName.Length - 1].Replace(".asset", "");
            generationPresetList.Add(itemNameSplit);
            nodePresetLookup.Add(itemNameSplit, AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(item), typeof(AutoGenerationGraph)));
        }
    }
    /// <summary>
    /// Runs the selected NodeGraph.
    /// </summary>
    /// <param name="graph">The NodeGraph to run.</param>
    public static void ParseNodeGraph(XNode.NodeGraph graph)
    {
        foreach (var node in graph.nodes)
        {
            if (node.name == "Start")
            {
                if (node.GetOutputPort("NextTask").GetConnections().Count == 0) // Check for start node being in graph but not linked.
                {
                    return;
                }
                XNode.Node nodeIteration = node.GetOutputPort("NextTask").Connection.node;
                if (nodeIteration != null)
                {
                    do
                    {
                        MethodInfo runNode = nodeIteration.GetType().GetMethod("RunNode");
                        runNode.Invoke(nodeIteration, null);
                        if (nodeIteration.GetOutputPort("NextTask").IsConnected)
                        {
                            nodeIteration = nodeIteration.GetOutputPort("NextTask").Connection.node;
                        }
                        else
                        {
                            nodeIteration = null;
                        }
                    }
                    while (nodeIteration != null);
                    MapIO.ChangeLandLayer(); // Puts the layer back to the one selected in MapIO LandLayer.
                }
            }
        }
    }
}


public class PrefabHierachy : TreeView
{
    MapIO mapIO = GameObject.FindGameObjectWithTag("MapIO").GetComponent<MapIO>();

    public PrefabHierachy(TreeViewState treeViewState)
        : base(treeViewState)
    {
        Reload();
    }
    protected override bool CanStartDrag(CanStartDragArgs args)
    {
        return true;
    }
    protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
    {
        DragAndDrop.PrepareStartDrag();
        if (args.draggedItemIDs[0] > 100) // Only drag prefabs not the treeview parents.
        {
            if (MapIO.prefabReference.ContainsKey(args.draggedItemIDs[0].ToString()))
            {
                Debug.Log("Prefab Found");
                MapIO.prefabReference.TryGetValue(args.draggedItemIDs[0].ToString(), out GameObject prefabDragged);
                GameObject newPrefab = prefabDragged;
                UnityEngine.Object[] objectArray = new UnityEngine.Object[1];
                objectArray[0] = newPrefab;
                DragAndDrop.objectReferences = objectArray;
                DragAndDrop.StartDrag("Prefab");
            }
            else
            {
                Debug.Log("Prefab not found.");
            }
        }
    }
    protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
    {
        if (args.performDrop)
        {
            Debug.Log("Dropped");
            return DragAndDropVisualMode.Copy;
        }
        return DragAndDropVisualMode.None;
    }
    Dictionary<string, TreeViewItem> treeviewParents = new Dictionary<string, TreeViewItem>();
    List<TreeViewItem> allItems = new List<TreeViewItem>();
    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
        allItems.Add(new TreeViewItem { id = 1, depth = 0, displayName = "Editor Tools" });
        // Add other editor shit like custom prefabs here.
        if (File.Exists("PrefabsLoaded.txt"))
        {
            var lines = File.ReadAllLines("PrefabsLoaded.txt");
            var parentId = -1000000; // Set this really low so it doesn't ever go into the positives or otherwise run into the risk of being the same id as a prefab.
            foreach (var line in lines)
            {
                var linesSplit = line.Split(':');
                var assetNameSplit = linesSplit[1].Split('/');
                for (int i = 0; i < assetNameSplit.Length; i++)
                {
                    var treePath = "";
                    for (int j = 0; j <= i; j++)
                    {
                        treePath += assetNameSplit[j];
                    }
                    if (!treeviewParents.ContainsKey(treePath))
                    {
                        var prefabName = assetNameSplit[assetNameSplit.Length - 1].Replace(".prefab", "");
                        var shortenedId = linesSplit[2].Substring(2);
                        if (i != assetNameSplit.Length - 1)
                        {
                            var treeviewItem = new TreeViewItem { id = parentId, depth = i, displayName = assetNameSplit[i] };
                            allItems.Add(treeviewItem);
                            treeviewParents.Add(treePath, treeviewItem);
                            parentId++;
                        }
                        else
                        {
                            var treeviewItem = new TreeViewItem { id = int.Parse(shortenedId), depth = i, displayName = prefabName };
                            allItems.Add(treeviewItem);
                            treeviewParents.Add(treePath, treeviewItem);
                        }
                    }
                }
            }
        }
        SetupParentsAndChildrenFromDepths(root, allItems);
        return root;
    }
}