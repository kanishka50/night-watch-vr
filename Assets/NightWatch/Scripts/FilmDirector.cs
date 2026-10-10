using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The story film. Runs a list of shots with its own camera (a normal
// camera, not the VR one): title cards on black, and camera shots that glide from one spot to
// another (or circle a point) with their own time of day and a caption in the bottom cinema bar.
// Actors are copies of the elephant model moved along set paths (no AI, no NavMesh); the herd
// really eats and flattens the rice, and the bull's hit shakes the hut (no damage).
// Night Watch > Story Film > Preview presses Play and runs the film once in the Game view.
// Night Watch > Story Film > Stills saves one picture per second of the film (for the contact
// sheet, tools/film_make.py sheet). Night Watch > Story Film > Record saves every frame at 30 fps
// (tools/film_make.py video makes the MP4). Both stop Play when done.
// While it runs the game stays quiet: no story, no START screen, the VR camera draws nothing.
public class FilmDirector : MonoBehaviour
{
    public enum Mode { Off, Preview, Stills, Record }

    [Serializable]
    public class Shot
    {
        public string name;
        public float seconds = 8f;

        [Header("Title card: black screen with one line")]
        public bool card;
        [TextArea] public string title;
        [TextArea] public string subtitle;
        [Tooltip("The big film title (the first card).")]
        public bool bigTitle;

        [Header("Camera glide: y is the height above the ground")]
        public Vector3 from;
        public Vector3 to;
        public Vector3 lookFrom;
        public Vector3 lookTo;

        [Header("Or camera circle (degrees: 0 north, 90 east)")]
        public bool orbit;
        [Tooltip("The point to circle and look at; y is the look height above the ground.")]
        public Vector3 center;
        public float radius = 30f;
        [Tooltip("Camera height above the ground at the centre.")]
        public float height = 18f;
        public float fromAngle;
        public float toAngle = 60f;

        [Header("Shot")]
        [Tooltip("The moment of the run for the light, 0 to 1 (like GameManager.DayProgress): 0 afternoon, 0.2 late afternoon, 0.25 dusk, 0.28 late dusk, 0.35 and later night.")]
        [Range(0f, 1f)] public float timeOfDay = 0.19f;
        [Tooltip("Multiplies the fog: less than 1 = clearer (wide drone shots).")]
        [Range(0.1f, 2f)] public float fog = 1f;
        [Tooltip("A short line in the bottom cinema bar.")]
        public string caption;
        [Tooltip("A straight cut from the shot before (no fade between them).")]
        public bool cutIn;
        [Tooltip("The oil lamp burns in the pela.")]
        public bool lamp;
        [Tooltip("Seconds into the shot when an elephant hits the hut: it shakes, dust flies (no damage). -1 = none.")]
        public float hutHitAt = -1f;
        [Tooltip("How much the camera shakes at the hut hit (m).")]
        public float shake;
    }

    // An elephant in one shot: walks (or runs) from one point to another between two moments of
    // the shot and stands at either end. Eating ones lower their heads into the rice.
    [Serializable]
    public class Actor
    {
        public string name;
        [Tooltip("The name of the shot it plays in.")]
        public string shot;
        [Tooltip("1 = an adult (as the game's elephants), 0.55 = a calf.")]
        public float size = 1f;
        public bool tusks;
        [Tooltip("Start and end points (x, z; standing on the ground).")]
        public Vector3 from;
        public Vector3 to;
        [Tooltip("Seconds into the shot when it leaves 'from' and reaches 'to'.")]
        public float moveStart;
        public float moveEnd = 8f;
        public bool run;
        [Tooltip("Eats the rice in front of it while in a crop plot.")]
        public bool eat;
        [Tooltip("Seconds into the shot when it rears up (Attack clip). -1 = never.")]
        public float attackAt = -1f;
    }

    // Night Watch > Story Film > Preview / Stills / Record puts the mode here for the next Play session.
    public const string ModeKey = "NightWatch.FilmMode";
    // The film screen (bars, cards, captions) is on Unity's UI layer; title cards draw only it.
    public const int ScreenLayer = 5;
    public const int FramesPerSecond = 30;

    public Shot[] shots;
    public Actor[] actors;

    [Header("Parts")]
    public Camera filmCamera;
    [Tooltip("Black over the whole picture, for fades and title cards.")]
    public Image fade;
    public CanvasGroup card;
    public TextMeshProUGUI cardTitle;
    public TextMeshProUGUI cardSubtitle;
    public TextMeshProUGUI caption;
    [Tooltip("The film's own oil lamp in the pela (off unless a shot asks for it).")]
    public GameObject lamp;
    public Light lampLight;
    [Tooltip("The elephant model to copy for the actors (the template's Model).")]
    public GameObject elephantModel;

    [Header("Look")]
    public float fadeSeconds = 0.8f;
    public float bigTitleSize = 120f;
    public float lineSize = 52f;
    [Tooltip("The caption appears this long after its shot starts.")]
    public float captionDelay = 1f;
    public float lampIntensity = 2.5f;
    [Tooltip("How much faster than in the game the film's herd eats, so it shows in a few seconds.")]
    public float eatSpeed = 2.5f;

    [Header("Saving pictures")]
    [Tooltip("Stills go to <folder>\\stills and frames to <folder>\\frames (C: is short of space, so D:).")]
    public string outputFolder = @"D:\NightWatchFilm";
    public int pictureWidth = 1920;
    public int pictureHeight = 1080;

    public Mode CurrentMode { get; private set; }
    public bool IsPlaying { get; private set; }
    public float FilmTime { get; private set; }
    public float Length
    {
        get
        {
            float total = 0f;
            if (shots != null) foreach (var s in shots) total += s.seconds;
            return total;
        }
    }

    // One actor in the scene while the film runs.
    class Cast
    {
        public Actor actor;
        public Transform root;
        public Animator animator;
        public string playing;
    }

    StoryIntro intro;
    StoryFilm storyFilm;
    DayNightCycle dayNight;
    Camera gameCamera;
    int gameCameraMask;
    bool startScreenWasShowing;
    Shot shown;
    Coroutine running;
    GameObject monitor;
    readonly List<Cast> cast = new List<Cast>();
    readonly HashSet<Shot> hutHitDone = new HashSet<Shot>();
    // Game parts hidden while the film runs (labels, board texts, the VR hands and their rays).
    readonly List<Behaviour> hiddenTexts = new List<Behaviour>();
    readonly List<Renderer> hiddenRenderers = new List<Renderer>();

    void Awake()
    {
        intro = FindAnyObjectByType<StoryIntro>();
        storyFilm = FindAnyObjectByType<StoryFilm>();
        dayNight = FindAnyObjectByType<DayNightCycle>();
        if (filmCamera != null) filmCamera.gameObject.SetActive(false);
        if (lamp != null) lamp.SetActive(false);

#if UNITY_EDITOR
        CurrentMode = (Mode)UnityEditor.SessionState.GetInt(ModeKey, (int)Mode.Off);
        UnityEditor.SessionState.EraseInt(ModeKey);
#endif
        if (CurrentMode == Mode.Off) return;
        // Before their own Start: the story intro and the film show in the game must not begin.
        if (intro != null) intro.play = false;
        if (storyFilm != null) storyFilm.play = false;
    }

    void Start()
    {
        if (CurrentMode == Mode.Preview) PlayFilm();
        else if (CurrentMode == Mode.Stills) SaveStills();
        else if (CurrentMode == Mode.Record) RecordFilm();
    }

    public void PlayFilm() => Begin(Run());

    public void SaveStills() => Begin(SavePictures(false));

    public void RecordFilm() => Begin(SavePictures(true));

    void Begin(IEnumerator job)
    {
        StopFilm();
        running = StartCoroutine(job);
    }

    // Before the film: let the game finish its own Start, end the story intro if it is on.
    IEnumerator Prepare()
    {
        yield return null;
        if (intro != null && intro.IsPlaying)
        {
            intro.Skip();
            while (intro.IsPlaying) yield return null;
        }
        Quiet(true);
        IsPlaying = true;
    }

    IEnumerator Run()
    {
        yield return Prepare();
        Debug.Log($"Night Watch film: playing {Length:0} s.");

        float length = Length;
        for (FilmTime = 0f; FilmTime < length; FilmTime += Time.deltaTime)
        {
            ShowAt(FilmTime);
            yield return null;
        }
        running = null;
        StopFilm();
        Debug.Log("Night Watch film: done.");
    }

    // Stills: one picture in the middle of every second, plus index.txt (picture, time, shot)
    // for the contact sheet. Record: every frame at 30 fps, with fixed time steps
    // (Time.captureFramerate), so slow drawing on the laptop does not matter.
    IEnumerator SavePictures(bool everyFrame)
    {
        yield return Prepare();
        string folder = Path.Combine(outputFolder, everyFrame ? "frames" : "stills");
        string prefix = everyFrame ? "frame_" : "still_";
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.GetFiles(folder, prefix + "*.jpg")) File.Delete(old);

        var target = new RenderTexture(pictureWidth, pictureHeight, 24);
        filmCamera.targetTexture = target;
        ShowMonitor(target);
        var picture = new Texture2D(pictureWidth, pictureHeight, TextureFormat.RGB24, false);
        var index = new List<string>();
        float length = Length;
        int count = everyFrame ? Mathf.CeilToInt(length * FramesPerSecond) : Mathf.CeilToInt(length - 0.5f);
        if (everyFrame) Time.captureFramerate = FramesPerSecond;
        Debug.Log($"Night Watch film: saving {count} pictures to {folder} ...");

        for (int i = 0; i < count; i++)
        {
            FilmTime = everyFrame ? (float)i / FramesPerSecond : i + 0.5f;
            var shot = ShowAt(FilmTime);
            if (!everyFrame)
            {
                // A jump: let the light and the plants' lists catch up, then show it again.
                for (int f = 0; f < 3; f++) yield return null;
                ShowAt(FilmTime);
            }
            yield return new WaitForEndOfFrame();

            RenderTexture.active = target;
            picture.ReadPixels(new Rect(0, 0, pictureWidth, pictureHeight), 0, 0);
            picture.Apply();
            RenderTexture.active = null;
            string file = $"{prefix}{i:00000}.jpg";
            if (!everyFrame) file = $"{prefix}{i:000}.jpg";
            File.WriteAllBytes(Path.Combine(folder, file), picture.EncodeToJPG(everyFrame ? 92 : 90));
            index.Add($"{file}\t{FilmTime:0.00}\t{shot.name}");
            if (everyFrame && i % (FramesPerSecond * 10) == 0)
                Debug.Log($"Night Watch film: recording, {FilmTime:0} of {length:0} s.");
            if (everyFrame) yield return null;
        }
        File.WriteAllLines(Path.Combine(folder, "index.txt"), index);

        Destroy(picture);
        running = null;
        StopFilm();
        target.Release();
        Destroy(target);
        Debug.Log($"Night Watch film: {index.Count} pictures saved in {folder}.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void StopFilm()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        Time.captureFramerate = 0;
        if (IsPlaying) Quiet(false);
        IsPlaying = false;
    }

    // While the film runs: the film camera draws, the VR camera draws nothing, the START screen
    // is hidden and the light follows the shots. Afterwards everything goes back.
    void Quiet(bool on)
    {
        if (on)
        {
            gameCamera = Camera.main;
            if (gameCamera != null)
            {
                gameCameraMask = gameCamera.cullingMask;
                gameCamera.cullingMask = 0;
            }
            HideGameParts();
            MakeCast();
            hutHitDone.Clear();
        }
        else
        {
            if (gameCamera != null) gameCamera.cullingMask = gameCameraMask;
            ShowGameParts();
            ClearCast();
            if (dayNight != null)
            {
                dayNight.progressOverride = -1f;
                dayNight.fogScale = 1f;
            }
            if (lamp != null) lamp.SetActive(false);
            if (filmCamera != null) filmCamera.targetTexture = null;
            if (monitor != null) Destroy(monitor);
        }
        if (filmCamera != null) filmCamera.gameObject.SetActive(on);
        var screen = StartScreen.Instance;
        if (screen != null && screen.panel != null)
        {
            if (on) startScreenWasShowing = screen.panel.activeSelf;
            screen.panel.SetActive(!on && startScreenWasShowing);
        }
        shown = null;
    }

    // While pictures are saved, the Game view shows them (otherwise it would stay dark).
    void ShowMonitor(Texture picture)
    {
        monitor = new GameObject("FilmMonitor");
        var canvas = monitor.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var image = new GameObject("Picture", typeof(RectTransform));
        image.transform.SetParent(monitor.transform, false);
        var rect = (RectTransform)image.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var raw = image.AddComponent<RawImage>();
        raw.texture = picture;
        raw.raycastTarget = false;
    }

    // Game props that look out of place in the story (a modern task board, the yellow climb
    // handles, the fence energiser and its wire). They stay in the game.
    static readonly string[] FilmHides = { "TaskBoard", "FenceEnergiser", "LadderTop", "LadderBottom" };

    // The game's floating labels and board texts, the props above, and everything on the VR rig
    // (hands, rays, the camera's own screens), so the film shows only the world.
    void HideGameParts()
    {
        hiddenTexts.Clear();
        hiddenRenderers.Clear();
        foreach (var text in FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None))
        {
            if (!text.enabled) continue;
            text.enabled = false;
            hiddenTexts.Add(text);
        }
        foreach (var name in FilmHides)
        {
            var prop = GameObject.Find(name);
            if (prop != null) HideRenderers(prop.transform);
        }
        if (gameCamera != null) HideRenderers(gameCamera.transform.root);
    }

    void HideRenderers(Transform root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hiddenRenderers.Add(r);
        }
    }

    void ShowGameParts()
    {
        foreach (var text in hiddenTexts) if (text != null) text.enabled = true;
        foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
        hiddenTexts.Clear();
        hiddenRenderers.Clear();
    }

    // A copy of the elephant model for each actor, hidden until its shot.
    void MakeCast()
    {
        ClearCast();
        if (elephantModel == null || actors == null) return;
        foreach (var actor in actors)
        {
            var root = new GameObject("FilmElephant_" + actor.name).transform;
            root.localScale = Vector3.one * actor.size;
            var copy = Instantiate(elephantModel, root);
            copy.transform.localPosition = elephantModel.transform.localPosition;
            copy.transform.localRotation = elephantModel.transform.localRotation;
            copy.transform.localScale = elephantModel.transform.localScale;
            copy.SetActive(true);
            // Tusks: the second material slot of the skin; no material there = not drawn.
            var skin = copy.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null && !actor.tusks && skin.sharedMaterials.Length >= 2)
                skin.sharedMaterials = new[] { skin.sharedMaterials[0] };
            var animator = copy.GetComponentInChildren<Animator>();
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            root.gameObject.SetActive(false);
            cast.Add(new Cast { actor = actor, root = root, animator = animator });
        }
    }

    void ClearCast()
    {
        foreach (var c in cast) if (c.root != null) Destroy(c.root.gameObject);
        cast.Clear();
    }

    // Puts everything where it is at this moment of the film and returns the shot.
    Shot ShowAt(float time)
    {
        float start = 0f;
        for (int i = 0; i < shots.Length; i++)
        {
            var shot = shots[i];
            bool last = i == shots.Length - 1;
            if (time < start + shot.seconds || last)
            {
                float t = time - start;
                // No fade out when the next shot is a straight cut.
                ShowShot(shot, t, last || !shots[i + 1].cutIn);
                ShowActors(shot, t);
                return shot;
            }
            start += shot.seconds;
        }
        return null;
    }

    void ShowShot(Shot shot, float t, bool fadeAtEnd)
    {
        // 0 to 1 at the start, 1 in the middle, 1 to 0 at the end.
        float left = Mathf.Clamp01((shot.seconds - t) / fadeSeconds);
        float fadeIn = shot.cutIn && !shot.card ? 1f : Mathf.Clamp01(t / fadeSeconds);
        float fadeOut = fadeAtEnd || shot.card ? left : 1f;
        float visible = Mathf.Min(fadeIn, fadeOut);

        if (shot != shown)
        {
            shown = shot;
            SetCard(shot);
            caption.text = shot.card ? "" : shot.caption;
            if (!shot.card && dayNight != null)
            {
                dayNight.progressOverride = shot.timeOfDay;
                dayNight.fogScale = shot.fog;
            }
            // A title card is only the screen: nothing of the world may show through.
            filmCamera.cullingMask = shot.card ? 1 << ScreenLayer : ~0;
            if (lamp != null) lamp.SetActive(!shot.card && shot.lamp);
        }

        if (shot.card)
        {
            SetFade(1f);
            card.alpha = visible;
            caption.alpha = 0f;
            return;
        }

        card.alpha = 0f;
        SetFade(1f - visible);
        caption.alpha = Mathf.Min(Mathf.Clamp01((t - captionDelay) / fadeSeconds), left);
        // A small flame flicker, from the film time (the same in every run).
        if (lampLight != null) lampLight.intensity = lampIntensity * (0.8f + 0.4f * Mathf.PerlinNoise(FilmTime * 7f, 0.3f));

        // The hut hit: once per run of the shot.
        if (shot.hutHitAt >= 0f && t >= shot.hutHitAt && hutHitDone.Add(shot))
            HitHut();

        // Slow at both ends, smooth in the middle.
        float e = Mathf.SmoothStep(0f, 1f, t / shot.seconds);
        Vector3 position, target;
        if (shot.orbit)
        {
            float a = Mathf.Lerp(shot.fromAngle, shot.toAngle, e) * Mathf.Deg2Rad;
            float ground = GroundY(shot.center);
            position = new Vector3(shot.center.x + Mathf.Sin(a) * shot.radius, ground + shot.height,
                shot.center.z + Mathf.Cos(a) * shot.radius);
            target = new Vector3(shot.center.x, ground + shot.center.y, shot.center.z);
        }
        else
        {
            position = Vector3.Lerp(Ground(shot.from), Ground(shot.to), e);
            target = Vector3.Lerp(Ground(shot.lookFrom), Ground(shot.lookTo), e);
        }
        // The camera shake of the hit, fading over 0.8 s.
        if (shot.shake > 0f && shot.hutHitAt >= 0f && t >= shot.hutHitAt)
        {
            float k = shot.shake * Mathf.Clamp01(1f - (t - shot.hutHitAt) / 0.8f);
            position += new Vector3(Mathf.PerlinNoise(FilmTime * 25f, 1f) - 0.5f, Mathf.PerlinNoise(FilmTime * 25f, 2f) - 0.5f, 0f) * 2f * k;
        }
        var direction = target - position;
        if (direction.sqrMagnitude > 0.001f)
            filmCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
    }

    // The bull hits the pela: it shakes and dust flies from the south side (no damage).
    void HitHut()
    {
        var hut = HutDamage.Instance;
        if (hut == null) return;
        hut.Shake(2.5f);
        Effects.Dust(hut.transform.position + new Vector3(0f, 1f, -2f), new Color(0.42f, 0.38f, 0.32f), 1.6f);
    }

    void ShowActors(Shot shot, float t)
    {
        foreach (var c in cast)
        {
            var a = c.actor;
            bool active = a.shot == shot.name;
            if (c.root.gameObject.activeSelf != active)
            {
                c.root.gameObject.SetActive(active);
                c.playing = null;
            }
            if (!active) continue;

            float k = a.moveEnd > a.moveStart ? Mathf.InverseLerp(a.moveStart, a.moveEnd, t) : 1f;
            var way = a.to - a.from;
            way.y = 0f;
            var position = Vector3.Lerp(a.from, a.to, k);
            position.y = GroundY(position);
            var rotation = way.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(way) : c.root.rotation;
            c.root.SetPositionAndRotation(position, rotation);

            float speed = a.moveEnd > a.moveStart ? way.magnitude / (a.moveEnd - a.moveStart) : 0f;
            bool moving = t > a.moveStart && t < a.moveEnd && speed > 0.05f;
            // Clip speeds as ElephantBody: the feet keep pace with the ground.
            if (a.attackAt >= 0f && t >= a.attackAt && t < a.attackAt + 1.6f) Play(c, "Attack", 1f);
            else if (moving && a.run) Play(c, "Run", Mathf.Clamp(speed / (4.6f * a.size), 0.6f, 1.5f));
            else if (moving) Play(c, "Walk", Mathf.Clamp(speed / (1.7f * a.size), 0.5f, 1.6f));
            // An eating elephant standing in the rice plays Eat (the model's own clip).
            else if (a.eat && CropPlot.At(position) != null) Play(c, "Eat", 1f);
            else Play(c, "Idle", 1f);

            // In the rice: the feet flatten it, and an eating elephant eats in front of it.
            var plot = CropPlot.At(position);
            if (plot == null) continue;
            plot.TrampleUnder(c.root);
            if (a.eat) plot.Eat(Time.deltaTime * eatSpeed, position + rotation * Vector3.forward * (2.4f * a.size));
        }
    }

    static void Play(Cast c, string state, float speed)
    {
        if (c.animator == null) return;
        if (c.playing != state)
        {
            if (c.playing == null) c.animator.Play(state, 0, 0f);
            else c.animator.CrossFadeInFixedTime(state, 0.25f);
            c.playing = state;
        }
        c.animator.speed = speed;
    }

    void SetCard(Shot shot)
    {
        cardTitle.text = shot.card ? shot.title : "";
        cardSubtitle.text = shot.card ? shot.subtitle : "";
        if (!shot.card) return;
        cardTitle.fontSize = shot.bigTitle ? bigTitleSize : lineSize;

        // The title sits above the middle, the subtitle below; centre the whole block.
        float width = cardTitle.rectTransform.sizeDelta.x;
        float titleHeight = cardTitle.GetPreferredValues(shot.title, width, 0f).y;
        float subtitleHeight = string.IsNullOrEmpty(shot.subtitle) ? 0f : cardSubtitle.GetPreferredValues(shot.subtitle, width, 0f).y;
        ((RectTransform)card.transform).anchoredPosition = new Vector2(0f, (subtitleHeight - titleHeight) * 0.5f);
    }

    void SetFade(float alpha)
    {
        var c = fade.color;
        c.a = alpha;
        fade.color = c;
    }

    static float GroundY(Vector3 p)
    {
        var terrain = Terrain.activeTerrain;
        return terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : 0f;
    }

    // A point whose y is the height above the ground there.
    static Vector3 Ground(Vector3 p)
    {
        p.y += GroundY(p);
        return p;
    }

    [ContextMenu("Test: play the film now")]
    void TestPlay() => PlayFilm();

    [ContextMenu("Test: save the stills now")]
    void TestStills() => SaveStills();

    [ContextMenu("Test: stop the film")]
    void TestStop() => StopFilm();
}
