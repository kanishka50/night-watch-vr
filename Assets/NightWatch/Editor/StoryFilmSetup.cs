using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

// Builds the film at the beginning (docs/story-film.md; called by Build Full Game and by
// Night Watch > Story Film > Add Film Parts): a VideoPlayer (full screen at run time), the sound
// cues, and StoryFilm, which plays it before the START screen. (It used to be shown on a cloth
// between bamboo poles in the field; now it is full screen.)
// The ola-leaf story (StoryIntro) is switched off: the film replaces it.
public static class StoryFilmSetup
{
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

        var player = root.gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        // Drawn over the whole view (StoryFilm sets the camera at run time).
        player.renderMode = VideoRenderMode.CameraNearPlane;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = false;
        player.skipOnDrop = true;
        player.waitForFirstFrame = true;
        film.player = player;

        // The film replaces the ola-leaf story (its code stays until the film is tested).
        var intro = game.Find("StoryIntro")?.GetComponent<StoryIntro>();
        if (intro != null) intro.play = false;
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
}
