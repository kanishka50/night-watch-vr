using System.Collections.Generic;
using UnityEngine;

// Switches a group of effects on for one part of the day and off again, fading in and out:
// particle emission, lights, sounds and (if asked) the visible parts. Used for the evening smoke,
// the fireflies, the oil lamps at the dagaba, the frogs and the falling leaves.
// Times are GameManager.DayProgress: 0 = start of the afternoon, about 0.22 = dusk begins,
// 0.31 = night, 0.96 = dawn, 1 = full morning. Off when the camera is far away (saves work).
public class TimeWindow : MonoBehaviour
{
    [Range(0f, 1f)] public float from = 0.25f;
    [Range(0f, 1f)] public float to = 0.95f;
    [Tooltip("How long the fade in and out takes (share of the whole day).")]
    public float fade = 0.015f;
    [Tooltip("Off when the camera is further than this (m).")]
    public float maxDistance = 90f;
    [Tooltip("Hide the visible parts (renderers) while off, e.g. the lamp flames.")]
    public bool hideWhenOff;
    [Tooltip("Test: 1 = always on, 0 = always off, -1 = follow the day.")]
    [Range(-1f, 1f)] public float testOverride = -1f;

    [Header("Now (read only)")]
    public float level;

    ParticleSystem[] particles;
    readonly List<(Light light, float intensity)> lights = new List<(Light, float)>();
    readonly List<(AudioSource source, float volume)> sounds = new List<(AudioSource, float)>();
    Renderer[] renderers;
    bool shown = true;

    void Start()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var l in GetComponentsInChildren<Light>(true)) lights.Add((l, l.intensity));
        foreach (var s in GetComponentsInChildren<AudioSource>(true)) sounds.Add((s, s.volume));
        renderers = GetComponentsInChildren<Renderer>(true);
        Apply(0f);
    }

    void Update()
    {
        float target = 0f;
        var game = GameManager.Instance;
        if (game != null)
        {
            float p = game.DayProgress;
            target = Mathf.Clamp01(Mathf.Min((p - from) / fade, (to - p) / fade));
        }
        if (testOverride >= 0f) target = testOverride;
        var cam = Camera.main;
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > maxDistance * maxDistance) target = 0f;
        if (Mathf.Abs(target - level) > 0.001f) Apply(Mathf.MoveTowards(level, target, Time.deltaTime * 0.5f));
    }

    void Apply(float value)
    {
        level = value;
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = value > 0.05f;
            if (value > 0.05f && !ps.isPlaying) ps.Play();
        }
        foreach (var (light, intensity) in lights)
        {
            light.intensity = intensity * value;
            light.enabled = value > 0.01f;
        }
        foreach (var (source, volume) in sounds)
        {
            source.volume = volume * value;
            if (value > 0.01f && !source.isPlaying && source.clip != null) source.Play();
            if (value <= 0.01f && source.isPlaying) source.Stop();
        }
        bool show = !hideWhenOff || value > 0.01f;
        if (show != shown)
        {
            shown = show;
            foreach (var r in renderers)
                if (!(r is ParticleSystemRenderer)) r.enabled = show;
        }
    }
}
