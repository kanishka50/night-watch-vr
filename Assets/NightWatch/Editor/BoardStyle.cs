using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// One look for every board and button, made to belong in the village:
//  - a board is an ola-leaf page (the dried palm-leaf paper of old Sri Lankan books) in a dark
//    wooden frame, with a wooden back (so nothing shows from behind)
//  - text is dark brown ink; titles are in Cinzel, deep red-brown
//  - a button is a wooden plank with gold Cinzel letters (QUIT is a red-brown plank)
// Used by the START board, the task board, the notice desk, the warning, the results and the
// credits. The page texture is made here in code (no download).
public static class BoardStyle
{
    public static readonly Color Ink = new Color(0.22f, 0.13f, 0.06f);
    public static readonly Color TitleInk = new Color(0.47f, 0.16f, 0.05f);
    public static readonly Color AlertInk = new Color(0.62f, 0.15f, 0.03f);
    public static readonly Color Gold = new Color(1f, 0.85f, 0.46f);

    const string PageTexturePath = TextureKit.Folder + "/OlaLeafPage.png";
    const string FontPath = "Assets/NightWatch/Fonts/Cinzel SDF.asset";

    // A framed page. center and rotation as EditorKit.Board (text in front, read looking along
    // the board's forward). warning = a red-brown frame (the hakka patas warning).
    public static GameObject Board(string name, Transform parent, Vector3 center, Quaternion rotation, Vector2 size,
        bool warning = false)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(center, rotation);

        var page = GameObject.CreatePrimitive(PrimitiveType.Quad);
        page.name = "Page";
        Object.DestroyImmediate(page.GetComponent<Collider>());
        page.transform.SetParent(root.transform, false);
        page.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        page.transform.localScale = new Vector3(size.x, size.y, 1f);
        var pageRenderer = page.GetComponent<Renderer>();
        pageRenderer.sharedMaterial = PageMaterial();
        pageRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // The frame: four wooden bars round the page, a little in front of it, and a wooden back.
        float bar = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.06f, 0.035f, 0.14f);
        var wood = EditorKit.Mat(warning ? "BoardFrameRed" : "BoardFrame", new Color(0.3f, 0.2f, 0.12f));
        float w = size.x + bar, h = size.y + bar;
        EditorKit.Part("FrameTop", PrimitiveType.Cube, root.transform, new Vector3(0f, size.y / 2f, 0.015f), new Vector3(w, bar, 0.05f), wood, false);
        EditorKit.Part("FrameBottom", PrimitiveType.Cube, root.transform, new Vector3(0f, -size.y / 2f, 0.015f), new Vector3(w, bar, 0.05f), wood, false);
        EditorKit.Part("FrameLeft", PrimitiveType.Cube, root.transform, new Vector3(-size.x / 2f, 0f, 0.015f), new Vector3(bar, h, 0.05f), wood, false);
        EditorKit.Part("FrameRight", PrimitiveType.Cube, root.transform, new Vector3(size.x / 2f, 0f, 0.015f), new Vector3(bar, h, 0.05f), wood, false);
        EditorKit.Part("Back", PrimitiveType.Cube, root.transform, new Vector3(0f, 0f, 0.05f), new Vector3(w, h, 0.03f), wood, false);
        return root;
    }

    // Body text on a page: dark brown ink.
    public static TextMeshPro BodyText(TextMeshPro text, bool alert = false)
    {
        text.color = alert ? AlertInk : Ink;
        return text;
    }

    // A title: Cinzel, deep red-brown.
    public static TextMeshPro Title(TextMeshPro text)
    {
        var font = Font();
        if (font != null) text.font = font;
        text.color = TitleInk;
        return text;
    }

    // A button's wooden plank (lit, with the weathered planks photo; TextureKit "ButtonWood").
    public static Material ButtonMaterial(bool red) =>
        EditorKit.Mat(red ? "ButtonWoodRed" : "ButtonWood", new Color(0.35f, 0.22f, 0.12f));

    // A button's letters: gold Cinzel with a dark outline, readable on the wood in any light.
    public static void ButtonText(TextMeshPro label)
    {
        var font = Font();
        if (font != null) label.font = font;
        label.color = Gold;
        label.fontStyle = FontStyles.Bold;
        if (font != null) label.fontSharedMaterial = Outline(font);
    }

    static TMP_FontAsset Font() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

    // A saved copy of Cinzel's material with a dark brown outline.
    static Material Outline(TMP_FontAsset font)
    {
        string path = $"{EditorKit.MaterialFolder}/CinzelOutline.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(font.material);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.12f, 0.06f, 0.02f));
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // The page: unlit (readable at night), with the ola-leaf picture.
    static Material PageMaterial()
    {
        var mat = EditorKit.Mat("OlaLeafPage", Color.white, true);
        mat.SetTexture("_BaseMap", PageTexture());
        mat.SetColor("_BaseColor", new Color(0.95f, 0.93f, 0.9f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Made once: warm cream palm leaf with fine fibres running along it, slow colour changes and
    // darker, older edges.
    static Texture2D PageTexture()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(PageTexturePath);
        if (existing != null) return existing;

        const int width = 512, height = 256;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        var paper = new Color(0.90f, 0.81f, 0.60f);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)width, v = y / (float)height;
            float fibres = (Mathf.PerlinNoise(u * 6f, v * 90f) - 0.5f) * 0.10f
                         + (Mathf.PerlinNoise(u * 40f + 7f, v * 260f) - 0.5f) * 0.06f;
            float blotch = (Mathf.PerlinNoise(u * 3f + 11f, v * 2.5f + 3f) - 0.5f) * 0.10f;
            float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 2f, Mathf.Min(v, 1f - v));
            float age = Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(0f, 1f, edge / 0.12f));
            float k = (1f + fibres + blotch) * age;
            pixels[y * width + x] = new Color(paper.r * k, paper.g * k, paper.b * (k - 0.03f));
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(PageTexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(PageTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(PageTexturePath);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(PageTexturePath);
    }
}
