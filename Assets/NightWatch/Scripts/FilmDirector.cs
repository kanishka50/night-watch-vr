using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The story film (docs/story-film.md). Runs a list of shots with its own camera (a normal
// camera, not the VR one): title cards on black, and camera shots that glide from one spot to
// another with their own time of day and a caption in the bottom cinema bar.
// Night Watch > Story Film > Preview presses Play and runs the film once in the Game view.
// While it runs the game stays quiet: no story intro, no START screen, the VR camera draws nothing.
public class FilmDirector : MonoBehaviour
{
    public enum Mode { Off, Preview }

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

        [Header("Camera shot: y is the height above the ground")]
        public Vector3 from;
        public Vector3 to;
        public Vector3 lookFrom;
        public Vector3 lookTo;
        [Tooltip("The moment of the run for the light, 0 to 1 (like GameManager.DayProgress): 0 afternoon, 0.2 late afternoon, 0.3 dusk, 0.35 and later night.")]
        [Range(0f, 1f)] public float timeOfDay = 0.19f;
        [Tooltip("A short line in the bottom cinema bar.")]
        public string caption;
    }

    // Night Watch > Story Film > Preview puts the mode here for the next Play session.
    public const string ModeKey = "NightWatch.FilmMode";

    public Shot[] shots;

    [Header("Parts")]
    public Camera filmCamera;
    [Tooltip("Black over the whole picture, for fades and title cards.")]
    public Image fade;
    public CanvasGroup card;
    public TextMeshProUGUI cardTitle;
    public TextMeshProUGUI cardSubtitle;
    public TextMeshProUGUI caption;

    [Header("Look")]
    public float fadeSeconds = 0.8f;
    public float bigTitleSize = 120f;
    public float lineSize = 52f;
    [Tooltip("The caption appears this long after its shot starts.")]
    public float captionDelay = 1f;

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

    void Awake()
    {
        intro = FindAnyObjectByType<StoryIntro>();
        dayNight = FindAnyObjectByType<DayNightCycle>();
        if (filmCamera != null) filmCamera.gameObject.SetActive(false);

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
        if (CurrentMode != Mode.Off) PlayFilm();
    }

    public void PlayFilm()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Let the game finish its own Start first (start spot, START screen).
        yield return null;
        if (intro != null && intro.IsPlaying)
        {
            intro.Skip();
            while (intro.IsPlaying) yield return null;
        }
        Quiet(true);
        IsPlaying = true;
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
        }
        else
        {
            if (gameCamera != null) gameCamera.cullingMask = gameCameraMask;
            if (dayNight != null) dayNight.progressOverride = -1f;
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

    // Puts everything where it is at this moment of the film. Uses only the time, so any moment
    // can be shown directly.
    void ShowAt(float time)
    {
        float start = 0f;
        foreach (var shot in shots)
        {
            if (time < start + shot.seconds)
            {
                ShowShot(shot, time - start);
                return;
            }
            start += shot.seconds;
        }
    }

    void ShowShot(Shot shot, float t)
    {
        // 0 to 1 at the start, 1 in the middle, 1 to 0 at the end.
        float fadeIn = Mathf.Clamp01(t / fadeSeconds);
        float fadeOut = Mathf.Clamp01((shot.seconds - t) / fadeSeconds);
        float visible = Mathf.Min(fadeIn, fadeOut);

        if (shot != shown)
        {
            shown = shot;
            SetCard(shot);
            caption.text = shot.card ? "" : shot.caption;
            if (!shot.card && dayNight != null) dayNight.progressOverride = shot.timeOfDay;
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
        caption.alpha = Mathf.Min(Mathf.Clamp01((t - captionDelay) / fadeSeconds), fadeOut);

        // Slow at both ends, smooth in the middle.
        float e = Mathf.SmoothStep(0f, 1f, t / shot.seconds);
        var position = Vector3.Lerp(Ground(shot.from), Ground(shot.to), e);
        var target = Vector3.Lerp(Ground(shot.lookFrom), Ground(shot.lookTo), e);
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

    // A point whose y is the height above the ground there.
    static Vector3 Ground(Vector3 p)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain != null) p.y += terrain.SampleHeight(p) + terrain.transform.position.y;
        return p;
    }

    [ContextMenu("Test: play the film now")]
    void TestPlay() => PlayFilm();

    [ContextMenu("Test: stop the film")]
    void TestStop() => StopFilm();
}
