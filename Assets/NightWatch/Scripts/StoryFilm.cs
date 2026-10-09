using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

// The story at the very beginning, before the START screen: our story film (docs/story-film.md),
// shown on a white cloth between two bamboo poles, like the open-air film shows of rural Sri
// Lanka. The player stands at the start spot facing the cloth, free to look around (the camera
// never moves by itself). The video is silent; the film's sounds are played here at set video
// times, so they stay in time. "Skip film" ends it at once. Then the player turns back to the
// hut (a fade) and the START screen appears.
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
    [Tooltip("The cloth, the poles and the skip sign. Shown only while the film plays.")]
    public GameObject screen;
    public Renderer picture;
    public VideoPlayer player;
    [Tooltip("Where the player stands to watch (facing the cloth).")]
    public Transform watchSpot;
    public Transform startSpot;
    [Tooltip("One-off sounds come from the cloth, loud up to this distance.")]
    public float soundDistance = 12f;

    public bool IsPlaying { get; private set; }

    bool skip;
    bool ended;
    readonly List<(AudioSource source, float until)> loops = new List<(AudioSource, float)>();

    void Awake()
    {
        if (screen != null) screen.SetActive(false);
    }

    void Start()
    {
        if (!play) return;
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
        // GroundPrep puts the player at the start spot in its Start; wait for that first.
        yield return null;
        ShowStartScreen(false);
        screen.SetActive(true);
        yield return MoveHead(watchSpot);

        var frame = new RenderTexture(1920, 1080, 0);
        player.targetTexture = frame;
        var material = picture.material;
        material.SetTexture("_BaseMap", frame);
        material.SetColor("_BaseColor", Color.white);

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
        player.targetTexture = null;
        frame.Release();
        Destroy(frame);
        yield return Finish();
    }

    void OnEnded(VideoPlayer source) => ended = true;

    IEnumerator Finish()
    {
        yield return MoveHead(startSpot);
        screen.SetActive(false);
        ShowStartScreen(true);
        IsPlaying = false;
    }

    // Fade out, stand at the spot in the dark, fade back in.
    static IEnumerator MoveHead(Transform spot)
    {
        var limit = PlayAreaLimit.Instance;
        if (spot == null || limit == null) yield break;
        limit.MoveHeadTo(spot.position, spot.rotation);
        while (limit.IsMoving) yield return null;
    }

    void Fire(Cue cue)
    {
        var clip = Clip(cue.sound);
        if (clip == null) return;
        if (cue.until <= 0f)
        {
            SoundBank.PlayAt(clip, picture.transform.position, cue.volume, soundDistance);
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
