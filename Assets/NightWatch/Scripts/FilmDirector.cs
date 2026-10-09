using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The story film (docs/story-film.md). Runs a list of shots with its own camera (a normal
// camera, not the VR one): title cards on black, and camera shots that glide from one spot to
// another (or circle a point) with their own time of day and a caption in the bottom cinema bar.
// Night Watch > Story Film > Preview presses Play and runs the film once in the Game view.
// Night Watch > Story Film > Stills saves one picture per second of the film (for the contact
// sheet, tools/film_make.py) and stops Play when done.
// While it runs the game stays quiet: no story intro, no START screen, the VR camera draws nothing.
public class FilmDirector : MonoBehaviour
{
    public enum Mode { Off, Preview, Stills }

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
    }

    // Night Watch > Story Film > Preview / Stills puts the mode here for the next Play session.
    public const string ModeKey = "NightWatch.FilmMode";
    // The film screen (bars, cards, captions) is on Unity's UI layer; title cards draw only it.
    public const int ScreenLayer = 5;

    public Shot[] shots;

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

    [Header("Look")]
    public float fadeSeconds = 0.8f;
    public float bigTitleSize = 120f;
    public float lineSize = 52f;
    [Tooltip("The caption appears this long after its shot starts.")]
    public float captionDelay = 1f;
    public float lampIntensity = 2.5f;

    [Header("Saving pictures")]
    [Tooltip("Stills go to <folder>\\stills (C: is short of space, so D:).")]
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

    StoryIntro intro;
    DayNightCycle dayNight;
    Camera gameCamera;
    int gameCameraMask;
    bool startScreenWasShowing;
    Shot shown;
    Coroutine running;
    // Game parts hidden while the film runs (labels, board texts, the VR hands and their rays).
    readonly List<Behaviour> hiddenTexts = new List<Behaviour>();
    readonly List<Renderer> hiddenRenderers = new List<Renderer>();

    void Awake()
    {
        intro = FindAnyObjectByType<StoryIntro>();
        dayNight = FindAnyObjectByType<DayNightCycle>();
        if (filmCamera != null) filmCamera.gameObject.SetActive(false);
        if (lamp != null) lamp.SetActive(false);

#if UNITY_EDITOR
        CurrentMode = (Mode)UnityEditor.SessionState.GetInt(ModeKey, (int)Mode.Off);
        UnityEditor.SessionState.EraseInt(ModeKey);
#endif
        if (CurrentMode == Mode.Off) return;
        // Before the story's own Start: it must not begin at all.
        if (intro != null) intro.play = false;
    }

    void Start()
    {
        if (CurrentMode == Mode.Preview) PlayFilm();
        else if (CurrentMode == Mode.Stills) SaveStills();
    }

    public void PlayFilm() => Begin(Run());

    public void SaveStills() => Begin(Stills());

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

    // One picture in the middle of every second of the film, saved as JPG, plus index.txt
    // (picture, time, shot) for the contact sheet.
    IEnumerator Stills()
    {
        yield return Prepare();
        string folder = Path.Combine(outputFolder, "stills");
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.GetFiles(folder, "still_*.jpg")) File.Delete(old);

        var target = new RenderTexture(pictureWidth, pictureHeight, 24);
        filmCamera.targetTexture = target;
        var picture = new Texture2D(pictureWidth, pictureHeight, TextureFormat.RGB24, false);
        var index = new List<string>();
        float length = Length;
        Debug.Log($"Night Watch film: saving {Mathf.CeilToInt(length)} stills to {folder} ...");

        for (int i = 0; i + 0.5f < length; i++)
        {
            FilmTime = i + 0.5f;
            var shot = ShowAt(FilmTime);
            // Let the light and the plants' lists catch up with the jump, then read the picture.
            for (int f = 0; f < 3; f++) yield return null;
            ShowAt(FilmTime);
            yield return new WaitForEndOfFrame();

            RenderTexture.active = target;
            picture.ReadPixels(new Rect(0, 0, pictureWidth, pictureHeight), 0, 0);
            picture.Apply();
            RenderTexture.active = null;
            string file = $"still_{i:000}.jpg";
            File.WriteAllBytes(Path.Combine(folder, file), picture.EncodeToJPG(90));
            index.Add($"{file}\t{FilmTime:0.0}\t{shot.name}");
        }
        File.WriteAllLines(Path.Combine(folder, "index.txt"), index);

        filmCamera.targetTexture = null;
        target.Release();
        Destroy(target);
        Destroy(picture);
        running = null;
        StopFilm();
        Debug.Log($"Night Watch film: {index.Count} stills saved in {folder}.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void StopFilm()
    {
        if (running != null) StopCoroutine(running);
        running = null;
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
        }
        else
        {
            if (gameCamera != null) gameCamera.cullingMask = gameCameraMask;
            ShowGameParts();
            if (dayNight != null)
            {
                dayNight.progressOverride = -1f;
                dayNight.fogScale = 1f;
            }
            if (lamp != null) lamp.SetActive(false);
            if (filmCamera != null) filmCamera.targetTexture = null;
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

    // The game's floating labels and board texts, and everything on the VR rig (hands, rays,
    // the camera's own screens), so the film shows only the world.
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
        if (gameCamera == null) return;
        foreach (var r in gameCamera.transform.root.GetComponentsInChildren<Renderer>())
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

    // Puts everything where it is at this moment of the film and returns the shot. Uses only
    // the time, so any moment can be shown directly.
    Shot ShowAt(float time)
    {
        float start = 0f;
        for (int i = 0; i < shots.Length; i++)
        {
            var shot = shots[i];
            bool last = i == shots.Length - 1;
            if (time < start + shot.seconds || last)
            {
                // No fade out when the next shot is a straight cut.
                ShowShot(shot, time - start, last || !shots[i + 1].cutIn);
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
        var direction = target - position;
        if (direction.sqrMagnitude > 0.001f)
            filmCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
    }

    void SetCard(Shot shot)
    {
        cardTitle.text = shot.card ? shot.title : "";
        cardSubtitle.text = shot.card ? shot.subtitle : "";
        if (!shot.card) return;
        cardTitle.fontSize = shot.bigTitle ? bigTitleSize : lineSize;

        // The title sits above the ornament, the subtitle below; centre the whole block.
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
