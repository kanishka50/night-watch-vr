using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

// The story at the very beginning, before the START screen: our story film,
// full screen as soon as the game starts. On the screen (simulator, Windows build) it fills the
// whole window: the black cinema bar baked into the top of the video is cut off and the sides are
// trimmed a little, so only the bottom bar with the captions stays black. On a headset the screen
// layer is not seen, so there the VideoPlayer draws on the camera's near plane, over black. The video is silent; the film's sounds are played
// here at set video times, so they stay in time. Space, Enter or the controller's A button skip it
// (an input action). Then the view fades in on the START board at the start spot.
// The video is Assets/StreamingAssets/NightWatchStory.mp4 (made by tools/film_make.py video);
// without it the game goes straight to the START screen.
public class StoryFilm : MonoBehaviour
{
    [Serializable]
    public class Cue
    {
        [Tooltip("Seconds of video.")]
        public float time;
        [Tooltip("A SoundBank sound: Birds, Crickets, Rumble, Trumpet, Bang, BranchCrack.")]
        public string sound;
        public float volume = 1f;
        [Tooltip("More than 0: loops (as background) until this video time.")]
        public float until;
    }

    [Tooltip("Off = straight to the START screen (also switched off while the film is recorded).")]
    public bool play = true;
    public string fileName = "NightWatchStory.mp4";
    public Cue[] cues;

    [Header("Parts")]
    public VideoPlayer player;
    public Transform startSpot;

    public bool IsPlaying { get; private set; }

    bool skip;
    bool ended;
    // The film plays once per launch: after Restart the next player goes straight to START.
    static bool seen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => seen = false;
    readonly List<(AudioSource source, float until)> loops = new List<(AudioSource, float)>();

    void Start()
    {
        if (!play || seen) return;
        seen = true;
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Night Watch: no story film at {path} (make it with tools/film_make.py video). Going straight to START.");
            return;
        }
        StartCoroutine(Show(path));
    }

    // Called by the "Skip film" button.
    public void Skip() => skip = true;

    IEnumerator Show(string path)
    {
        IsPlaying = true;
        // Black at once; GroundPrep puts the player at the start spot in its Start, under the black.
        ScreenFeedback.FadeOut(0.01f);
        ShowStartScreen(false);
        var skipAction = new InputAction("Skip film", InputActionType.Button, "<Keyboard>/space");
        skipAction.AddBinding("<Keyboard>/enter");
        skipAction.AddBinding("<XRController>/primaryButton");
        skipAction.performed += _ => skip = true;
        skipAction.Enable();
        var note = SkipNote();
        yield return null;

        RenderTexture frame = null;
        GameObject filmScreen = null;
        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            player.renderMode = VideoRenderMode.CameraNearPlane;
            player.targetCamera = Camera.main;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.targetCameraAlpha = 1f;
        }
        else
        {
            frame = new RenderTexture(VideoWidth, VideoHeight, 0);
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = frame;
            filmScreen = FilmScreen(frame);
        }
        player.url = path;
        player.loopPointReached += OnEnded;
        player.Prepare();
        for (float wait = 0f; !player.isPrepared && wait < 15f && !skip; wait += Time.deltaTime)
            yield return null;

        if (player.isPrepared && !skip)
        {
            player.Play();
            int next = 0;
            while (!ended && !skip)
            {
                float time = (float)player.time;
                while (cues != null && next < cues.Length && cues[next].time <= time) Fire(cues[next++]);
                StopLoops(time);
                yield return null;
            }
        }
        else if (!skip) Debug.LogWarning("Night Watch: the story film did not load. Going to START.");

        player.loopPointReached -= OnEnded;
        player.Stop();
        StopLoops(float.MaxValue);
        skipAction.Disable();
        skipAction.Dispose();
        if (note != null) Destroy(note);
        if (filmScreen != null) Destroy(filmScreen);
        if (frame != null)
        {
            player.targetTexture = null;
            frame.Release();
            Destroy(frame);
        }
        yield return Finish();
    }

    // The video file: 1280 x 720 with black cinema bars baked in, 92 pixels at the top and at the
    // bottom (the captions are in the bottom one).
    const int VideoWidth = 1280, VideoHeight = 720, TopBar = 92;

    // A screen layer that the video fills: the top bar is cut off, and the picture is scaled to
    // cover the whole window (the sides are trimmed a little on a 16:9 screen).
    static GameObject FilmScreen(RenderTexture frame)
    {
        var go = new GameObject("FilmScreen");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;   // under the skip note (100)

        var black = new GameObject("Black").AddComponent<Image>();
        black.transform.SetParent(go.transform, false);
        black.color = Color.black;
        var blackRect = black.rectTransform;
        blackRect.anchorMin = Vector2.zero;
        blackRect.anchorMax = Vector2.one;
        blackRect.offsetMin = blackRect.offsetMax = Vector2.zero;

        var picture = new GameObject("Picture").AddComponent<RawImage>();
        picture.transform.SetParent(go.transform, false);
        picture.texture = frame;
        float kept = (VideoHeight - TopBar) / (float)VideoHeight;
        picture.uvRect = new Rect(0f, 0f, 1f, kept);   // UV 0 is the bottom row
        var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = VideoWidth / (VideoHeight * kept);
        return go;
    }

    void OnEnded(VideoPlayer source) => ended = true;

    // Back at the start spot facing the START board, then the black fades away.
    IEnumerator Finish()
    {
        var limit = PlayAreaLimit.Instance;
        if (startSpot != null && limit != null)
            limit.MoveHeadTo(startSpot.position, startSpot.rotation, onGround: true, fadeFirst: false);
        ShowStartScreen(true);
        yield return null;
        ScreenFeedback.FadeIn(0.8f);
        IsPlaying = false;
    }

    // "Space: skip film" in the corner of the screen while the film plays (a screen overlay, drawn
    // after the film; a headset does not show it, there the A button skips).
    static GameObject SkipNote()
    {
        var go = new GameObject("SkipFilmNote");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "Space: skip film";
        text.fontSize = 22f;
        text.color = new Color(1f, 1f, 1f, 0.75f);
        text.alignment = TextAlignmentOptions.BottomRight;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-30f, 24f);
        rect.sizeDelta = new Vector2(400f, 40f);
        return go;
    }

    void Fire(Cue cue)
    {
        var clip = Clip(cue.sound);
        if (clip == null) return;
        if (cue.until <= 0f)
        {
            // Full screen, so the sounds are played at the player's head.
            var head = PlayerState.Head;
            SoundBank.PlayAt(clip, head != null ? head.position : transform.position, cue.volume, 12f);
            return;
        }
        // A background loop (birds, crickets), the same in both ears.
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = cue.volume;
        source.Play();
        loops.Add((source, cue.until));
    }

    void StopLoops(float time)
    {
        for (int i = loops.Count - 1; i >= 0; i--)
        {
            if (loops[i].until > time) continue;
            if (loops[i].source != null) Destroy(loops[i].source);
            loops.RemoveAt(i);
        }
    }

    static AudioClip Clip(string sound)
    {
        switch (sound)
        {
            case "Birds": return SoundBank.Birds;
            case "Crickets": return SoundBank.Crickets;
            case "Rumble": return SoundBank.Rumble;
            case "Trumpet": return SoundBank.Trumpet;
            case "Bang": return SoundBank.Bang;
            case "BranchCrack": return SoundBank.BranchCrack;
            default:
                Debug.LogWarning($"Night Watch: StoryFilm has no sound called {sound}.");
                return null;
        }
    }

    static void ShowStartScreen(bool show)
    {
        var start = StartScreen.Instance;
        if (start != null && start.panel != null) start.panel.SetActive(show);
    }

    [ContextMenu("Test: skip the film")]
    void TestSkip() => Skip();
}
