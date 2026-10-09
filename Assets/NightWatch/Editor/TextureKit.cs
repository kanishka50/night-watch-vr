using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Real surface textures for the coded shapes (free CC0 images from Poly Haven, 1k size).
// A material whose name is in the list below gets a photo texture and its normal map (small
// bumps and cracks that catch the light). If an image is missing, the plain colour stays.
// FitUVs then lays each texture out by the shape's real size in metres, so a long post and a
// small box show the wood at the same scale, and wood grain runs along the long side.
public static class TextureKit
{
    public const string Folder = "Assets/NightWatch/Textures";

    struct Look
    {
        public string image;   // file name in Folder, without "_diff_1k.jpg"
        public float tile;     // metres covered by one copy of the image
        public Color tint;     // multiplies the image (white = as it is)
        public float smooth;   // 0 = dull, 1 = shiny

        public Look(string image, float tile, Color tint, float smooth)
        {
            this.image = image;
            this.tile = tile;
            this.tint = tint;
            this.smooth = smooth;
        }
    }

    // Material name > look. Added one group at a time, checking the speed after each step.
    static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
    {
        // Step 1: the hut (stilts, floor, rails, ladder, table, boards) and its roof.
        { "Wood", new Look("weathered_planks", 1.5f, Color.white, 0.1f) },
        // The thatch photo is grey reed; a straw tint makes it look like a dry paddy-straw roof.
        { "Thatch", new Look("thatch_roof_angled", 2f, new Color(1f, 0.82f, 0.5f), 0.05f) },
        // The pela, restyled to match the village (old and hand-made): rough bark poles, grey old
        // planks, and old cadjan (dried coconut leaves): the reed photo tinted grey-brown.
        { "HutPole", new Look("bark_brown_02", 1.2f, new Color(0.85f, 0.8f, 0.75f), 0.05f) },
        { "HutFloor", new Look("weathered_planks", 1.5f, new Color(0.78f, 0.72f, 0.65f), 0.05f) },
        { "Cadjan", new Look("thatch_roof_angled", 1.5f, new Color(0.78f, 0.62f, 0.42f), 0.02f) },
        // The pela's illuk grass thatch: the reed photo in a dry golden straw colour, at a finer
        // scale than the old cadjan, so the stems read from the ground.
        { "PelaThatch", new Look("thatch_roof_angled", 1.1f, new Color(1f, 0.84f, 0.55f), 0.03f) },
    };

    // Puts the texture on the material (called every time a tool asks for the material).
    public static void Apply(Material mat, string name)
    {
        if (mat == null || !Looks.TryGetValue(name, out var look)) return;
        var albedo = Load(look.image + "_diff_1k", false);
        if (albedo == null) return;
        var normal = Load(look.image + "_nor_gl_1k", true);

        mat.SetTexture("_BaseMap", albedo);
        mat.SetTextureScale("_BaseMap", Vector2.one / look.tile);
        mat.SetColor("_BaseColor", look.tint);
        mat.SetFloat("_Smoothness", look.smooth);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(mat);
    }

    // Loads an image and keeps its import settings light: 1k at most, normal maps marked as such.
    static Texture2D Load(string file, bool normalMap)
    {
        string path = $"{Folder}/{file}.jpg";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.maxTextureSize != 1024 || importer.anisoLevel != 4)
        {
            importer.textureType = type;
            importer.maxTextureSize = 1024;
            importer.anisoLevel = 4; // keeps the floor and ground sharp when seen at a low angle
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Unity's cube and cylinder stretch one image over each face, whatever the size. This gives
    // every textured cube or cylinder in the scene a copy of the shape with UVs (the map of where
    // the image goes) measured in metres. Shapes of the same size share one copy.
    public static void FitUVs()
    {
        var copies = new Dictionary<string, Mesh>();
        int changed = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var mat = r.sharedMaterial;
            if (mat == null || !Looks.ContainsKey(mat.name) || mat.GetTexture("_BaseMap") == null) continue;
            var filter = r.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || (mesh.name != "Cube" && mesh.name != "Cylinder")) continue;
            if (AssetDatabase.GetAssetPath(mesh) != "Library/unity default resources") continue;

            var s = r.transform.lossyScale;
            var size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            string key = $"{mesh.name}_{size.x:0.00}x{size.y:0.00}x{size.z:0.00}";
            if (!copies.TryGetValue(key, out var copy))
            {
                copy = mesh.name == "Cube" ? CubeCopy(mesh, size) : CylinderCopy(mesh, size);
                copy.name = key;
                copies[key] = copy;
            }
            filter.sharedMesh = copy;
            changed++;
        }
        Debug.Log($"Night Watch: textures fitted on {changed} shapes ({copies.Count} shape sizes).");
    }

    // Each face gets the image flat on it, the image's up direction along the face's longer side.
    static Mesh CubeCopy(Mesh source, Vector3 size)
    {
        var copy = Object.Instantiate(source);
        var points = source.vertices;
        var normals = source.normals;
        var uv = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var p = Vector3.Scale(points[i], size);
            var n = normals[i];
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            // The two axes that lie flat on this face.
            int a, b;
            if (ax >= ay && ax >= az) { a = 2; b = 1; }
            else if (ay >= az) { a = 0; b = 2; }
            else { a = 0; b = 1; }
            // b is the long side: the image's up direction (wood grain) runs along it.
            if (size[a] > size[b]) { int t = a; a = b; b = t; }
            uv[i] = new Vector2(p[a], p[b]);
        }
        copy.uv = uv;
        return copy;
    }

    // Unity's cylinder is 2 units tall and 1 wide. Its side UVs already go once round (u) and
    // once up (v), so they only need scaling to metres. The two end caps are mapped flat.
    static Mesh CylinderCopy(Mesh source, Vector3 size)
    {
        var copy = Object.Instantiate(source);
        var points = source.vertices;
        var normals = source.normals;
        var oldUV = source.uv;
        var uv = new Vector2[points.Length];
        float round = Mathf.PI * (size.x + size.z) / 2f;
        float height = 2f * size.y;
        for (int i = 0; i < points.Length; i++)
        {
            if (Mathf.Abs(normals[i].y) > 0.9f)
                uv[i] = new Vector2(points[i].x * size.x, points[i].z * size.z);
            else
                uv[i] = new Vector2(oldUV[i].x * round, oldUV[i].y * height);
        }
        copy.uv = uv;
        return copy;
    }
}
