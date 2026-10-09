using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Builds the story film's director (docs/story-film.md): the film camera, the screen on it
// (cinema bars, the black fade, the title card in gold with a small ornament, the caption in the
// bottom bar) and the shot list. Called by Night Watch > Build Full Game, and on its own by
// Night Watch > Story Film > Add Film Director (no full rebuild needed).
// Night Watch > Story Film > Preview presses Play and runs the film once in the Game view.
public static class FilmDirectorSetup
{
    const string GameScene = "Assets/Scenes/NightWatch.unity";
    const string FontFile = "Assets/NightWatch/Fonts/Cinzel.ttf";
    const string FontAssetPath = "Assets/NightWatch/Fonts/Cinzel SDF.asset";

    // The picture is 1920 x 1080 (16 : 9); bars make it 2.39 : 1 inside.
    const float Width = 1920f, Height = 1080f;
    const float BarHeight = (Height - Width / 2.39f) * 0.5f;

    static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
    static readonly Color PaleGold = new Color(0.88f, 0.82f, 0.68f);
    static readonly Color CaptionGrey = new Color(0.80f, 0.80f, 0.80f);

    // Shot list (story-film.md section 4). Camera points: x and z in the world, y above the ground.
    static FilmDirector.Shot[] Shots() => new[]
    {
        new FilmDirector.Shot
        {
            name = "0 Title", seconds = 5f, card = true, bigTitle = true,
            title = "NIGHT WATCH", subtitle = "A night in the dry zone of Sri Lanka",
        },
        new FilmDirector.Shot
        {
            name = "1 The tank at sunset", seconds = 8f,
            from = new Vector3(-20f, 25f, 90f), to = new Vector3(-5f, 10f, 30f),
            lookFrom = new Vector3(-12f, 0f, 45f), lookTo = new Vector3(0f, 1f, -5f),
            timeOfDay = 0.19f, caption = "An old village beside its tank (wewa)",
        },
    };

    [MenuItem("Night Watch/Story Film/Add Film Director")]
    static void AddFilmDirector()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Watch", "Stop Play mode first.", "OK");
            return;
        }
        if (EditorSceneManager.GetActiveScene().path != GameScene)
        {
            EditorUtility.DisplayDialog("Night Watch", $"Open {GameScene} first (File > Open Scene).", "OK");
            return;
        }
        var game = GameObject.Find("Game");
        if (game == null)
        {
            EditorUtility.DisplayDialog("Night Watch", "No Game object in the scene. Run Night Watch > Build Full Game first.", "OK");
            return;
        }
        var old = game.transform.Find("FilmDirector");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        Build(game.transform);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Night Watch: film director added. Save the scene with Ctrl+S.");
    }

    [MenuItem("Night Watch/Story Film/Preview")]
    static void Preview()
    {
        var director = Object.FindAnyObjectByType<FilmDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            EditorUtility.DisplayDialog("Night Watch", "No film director in the scene. Run Night Watch > Story Film > Add Film Director first.", "OK");
            return;
        }
        if (EditorApplication.isPlaying)
        {
            director.PlayFilm();
            return;
        }
        SessionState.SetInt(FilmDirector.ModeKey, (int)FilmDirector.Mode.Preview);
        EditorApplication.isPlaying = true;
    }

    public static void Build(Transform game)
    {
        var root = EditorKit.Group("FilmDirector", game);
        var director = root.gameObject.AddComponent<FilmDirector>();
        director.shots = Shots();

        // The film camera: a normal camera above the VR one (drawn last, so it fills the view).
        var camGo = new GameObject("FilmCamera");
        camGo.transform.SetParent(root, false);
        var cam = camGo.AddComponent<Camera>();
        cam.depth = 50f;
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 800f;
        var data = cam.GetUniversalAdditionalCameraData();
        var main = Camera.main;
        data.renderPostProcessing = main != null && main.GetUniversalAdditionalCameraData().renderPostProcessing;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        director.filmCamera = cam;

        BuildScreen(director, cam);
        camGo.SetActive(false);
    }

    // The screen in front of the film camera, drawn into the same picture.
    static void BuildScreen(FilmDirector director, Camera cam)
    {
        var font = FilmFont();

        var canvasGo = new GameObject("FilmScreen", typeof(RectTransform));
        canvasGo.transform.SetParent(cam.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.5f;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(Width, Height);
        scaler.matchWidthOrHeight = 1f;
        var screen = canvasGo.transform;

        // Order matters: later children are drawn on top.
        director.fade = Box("Fade", screen, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.black);
        Box("BarTop", screen, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -BarHeight), Vector2.zero, Color.black);
        Box("BarBottom", screen, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, BarHeight), Color.black);

        // The title card: the line above, a thin gold line with a lotus-like row of dots, then
        // the small line below.
        var cardRect = Rect("Card", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        director.card = cardRect.gameObject.AddComponent<CanvasGroup>();
        director.card.alpha = 0f;

        director.cardTitle = Text("Title", cardRect, new Vector2(0f, 30f), new Vector2(0.5f, 0f), font, Gold, 52f, 6f,
            TextAlignmentOptions.Bottom);
        director.cardSubtitle = Text("Subtitle", cardRect, new Vector2(0f, -32f), new Vector2(0.5f, 1f), font, PaleGold, 36f, 3f,
            TextAlignmentOptions.Top);

        var ornament = Rect("Ornament", cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Box("Line", ornament, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220f, -1f), new Vector2(220f, 1f), Gold);
        Dot(ornament, 0f, 16f);
        foreach (var x in new[] { -28f, 28f }) Dot(ornament, x, 8f);
        foreach (var x in new[] { -232f, 232f }) Dot(ornament, x, 6f);

        // The caption, in the bottom bar.
        director.caption = Text("Caption", screen, Vector2.zero, new Vector2(0.5f, 0.5f), font, CaptionGrey, 30f, 2f,
            TextAlignmentOptions.Center);
        var captionRect = director.caption.rectTransform;
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = new Vector2(1f, 0f);
        captionRect.offsetMin = Vector2.zero;
        captionRect.offsetMax = new Vector2(0f, BarHeight);
        director.caption.text = "";
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    // A plain coloured rectangle.
    static Image Box(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var image = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // A small gold diamond on the ornament line.
    static void Dot(Transform parent, float x, float size)
    {
        var dot = Box("Dot", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-size, -size) * 0.5f,
            new Vector2(size, size) * 0.5f, Gold);
        dot.rectTransform.anchoredPosition = new Vector2(x, 0f);
        dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    static TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 pivot, TMP_FontAsset font,
        Color color, float size, float spacing, TextAlignmentOptions align)
    {
        var rect = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(1500f, 400f);
        rect.anchoredPosition = position;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.color = color;
        text.fontSize = size;
        text.characterSpacing = spacing;
        text.alignment = align;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    // Cinzel (Google Fonts, SIL Open Font License), made into a TextMeshPro font asset once.
    // Dynamic: letters are added to its picture (atlas) when a text first needs them.
    static TMP_FontAsset FilmFont()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (asset != null) return asset;
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontFile);
        if (font == null)
        {
            Debug.LogWarning($"Night Watch: {FontFile} is missing; the film uses the default font.");
            return null;
        }
        asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = "Cinzel SDF";
        AssetDatabase.CreateAsset(asset, FontAssetPath);
        asset.atlasTextures[0].name = "Cinzel SDF Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = "Cinzel SDF Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        asset.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;'!?()-");
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }
}
