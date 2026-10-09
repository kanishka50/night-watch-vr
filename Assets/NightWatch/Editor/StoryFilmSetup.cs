using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the film show at the beginning (docs/story-film.md; called by Build Full Game and by
// Night Watch > Story Film > Add Film Parts): a white cloth on two bamboo poles 7 m south of the
// start spot (the hut is north of it), the picture on the cloth with a VideoPlayer, a "Skip film"
// sign by the player, the sound cues, and StoryFilm, which plays it before the START screen.
// The ola-leaf story (StoryIntro) is switched off: the film replaces it.
public static class StoryFilmSetup
{
    // The video picture (16 : 9, the cinema bars are in the video) and the cloth around it.
    const float PictureWidth = 4f, PictureHeight = 2.25f;
    const float ClothMargin = 0.2f;
    const float ScreenDistance = 7f;     // south of the start spot
    const float PictureCenterHeight = 2.6f;
    const float PoleHeight = 4.1f;

    static readonly Color Cloth = new Color(0.93f, 0.91f, 0.86f);
    static readonly Color Bamboo = new Color(0.62f, 0.55f, 0.32f);

    // The film's sounds by shot (FilmDirectorSetup.Shots): shot, seconds into it, sound, volume,
    // and for a background loop the shot it stops at ("" = a one-off sound; "end" = to the end).
    static readonly (string shot, float at, string sound, float volume, string until)[] Sounds =
    {
        ("0", 0.5f, "Birds", 0.25f, "5"),
        ("5", 1f, "Crickets", 0.35f, "end"),
        ("6", 1.5f, "Rumble", 1f, ""),
        ("6", 5.5f, "Rumble", 0.8f, ""),
        ("7", 2f, "BranchCrack", 0.6f, ""),
        ("8a", 0.3f, "Rumble", 1f, ""),
        ("8a", 2.2f, "Trumpet", 0.9f, ""),
        ("8b", FilmDirectorSetup.BullHitAt, "Bang", 1f, ""),
        ("8b", FilmDirectorSetup.BullHitAt + 0.4f, "Trumpet", 0.8f, ""),
    };

    public static void Build(Transform game)
    {
        var root = EditorKit.Group("StoryFilm", game);
        var film = root.gameObject.AddComponent<StoryFilm>();
        film.cues = Cues();

        film.startSpot = game.Find("GroundPrep/StartSpot");
        if (film.startSpot == null)
        {
            Debug.LogWarning("Night Watch: StoryFilm has no start spot (Game/GroundPrep/StartSpot). Build the whole game.");
            return;
        }
        var start = film.startSpot.position;

        // The player stands at the start spot but faces south, to the cloth.
        film.watchSpot = EditorKit.Empty("WatchSpot", root, start);
        film.watchSpot.rotation = Quaternion.Euler(0f, 180f, 0f);

        var screen = EditorKit.Group("Screen", root);
        screen.position = FarmLayout.OnGround(start + Vector3.back * ScreenDistance);
        film.screen = screen.gameObject;
        // Read by a player looking south (along -z).
        var facing = Quaternion.LookRotation(Vector3.back);
        var center = screen.position + Vector3.up * PictureCenterHeight;

        EditorKit.Board("Cloth", screen, center, facing,
            new Vector2(PictureWidth + 2f * ClothMargin, PictureHeight + 2f * ClothMargin), Cloth);
        film.picture = Picture(screen, center, facing);
        // The cloth is solid from the ground to its top edge: nobody walks through or under it.
        float clothTop = PictureCenterHeight + PictureHeight * 0.5f + ClothMargin;
        EditorKit.BodyWall("Solid", screen, screen.position + Vector3.up * (clothTop * 0.5f), facing,
            new Vector3(PictureWidth + 2f * ClothMargin + 0.2f, clothTop, 0.2f));

        var bamboo = EditorKit.Mat("Bamboo", Bamboo);
        float poleX = PictureWidth * 0.5f + ClothMargin + 0.08f;
        foreach (var x in new[] { -poleX, poleX })
            EditorKit.Part("Pole", PrimitiveType.Cylinder, screen, new Vector3(x, PoleHeight * 0.5f, 0f),
                new Vector3(0.09f, PoleHeight * 0.5f, 0.09f), bamboo);
        var bar = EditorKit.Part("TopBar", PrimitiveType.Cylinder, screen, new Vector3(0f, PictureCenterHeight + PictureHeight * 0.5f + ClothMargin, 0.02f),
            new Vector3(0.04f, poleX, 0.04f), bamboo, false);
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        var player = root.gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = false;
        player.skipOnDrop = true;
        player.waitForFirstFrame = true;
        film.player = player;

        BuildSkipSign(screen, film, start);

        // The film replaces the ola-leaf story (its code stays until the film is tested).
        var intro = game.Find("StoryIntro")?.GetComponent<StoryIntro>();
        if (intro != null) intro.play = false;

        screen.gameObject.SetActive(false);
    }

    static StoryFilm.Cue[] Cues()
    {
        float length = FilmDirectorSetup.FilmLength();
        var cues = new StoryFilm.Cue[Sounds.Length];
        for (int i = 0; i < Sounds.Length; i++)
        {
            var s = Sounds[i];
            float until = s.until == "" ? 0f : s.until == "end" ? length + 1f : FilmDirectorSetup.ShotStart(s.until);
            cues[i] = new StoryFilm.Cue { time = FilmDirectorSetup.ShotStart(s.shot) + s.at, sound = s.sound, volume = s.volume, until = until };
        }
        System.Array.Sort(cues, (a, b) => a.time.CompareTo(b.time));
        return cues;
    }

    // The quad the video is drawn on (unlit, black until the film starts), just in front of the cloth.
    static Renderer Picture(Transform screen, Vector3 center, Quaternion facing)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Picture";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(screen, false);
        go.transform.SetPositionAndRotation(center - facing * Vector3.forward * 0.01f, facing);
        go.transform.localScale = new Vector3(PictureWidth, PictureHeight, 1f);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = EditorKit.Mat("FilmPicture", Color.black, true);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    // A small post with "Skip film" low on the right, 1.3 m in front of the player.
    static void BuildSkipSign(Transform screen, StoryFilm film, Vector3 start)
    {
        var facing = Quaternion.LookRotation(Vector3.back);
        var ground = FarmLayout.OnGround(start + new Vector3(-0.8f, 0f, -1.3f));
        var sign = EditorKit.Group("SkipSign", screen);
        sign.position = ground;

        EditorKit.Part("Post", PrimitiveType.Cylinder, sign, new Vector3(0f, 0.45f, 0f), new Vector3(0.06f, 0.45f, 0.06f),
            EditorKit.Mat("Bamboo", Bamboo), false);
        var buttonPos = ground + new Vector3(0f, 0.95f, 0f);
        var button = EditorKit.Part("SkipButton", PrimitiveType.Cube, sign, new Vector3(0f, 0.95f, 0f), new Vector3(0.42f, 0.12f, 0.04f),
            EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)));
        var box = button.GetComponent<BoxCollider>();
        box.size = new Vector3(1.2f, 2f, 2f);
        button.AddComponent<XRSimpleInteractable>();
        var skip = button.AddComponent<SkipStoryButton>();
        skip.film = film;
        skip.highlightColor = new Color(1f, 1f, 0.2f);
        // On the player's side of the board (north, +z).
        EditorKit.Text("SkipLabel", sign, buttonPos + new Vector3(0f, 0f, 0.03f), facing, new Vector2(0.38f, 0.09f),
            "Skip film", 0.8f, TextAlignmentOptions.Center);
    }
}
