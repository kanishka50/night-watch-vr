using UnityEngine;

// A looping 3D sound made in code (SoundBank), heard only near this place. A TimeWindow on the
// same object (or a parent) decides when it plays; without one it plays all the time.
[RequireComponent(typeof(AudioSource))]
public class LoopSound : MonoBehaviour
{
    public enum Clip { Frogs, Trickle }
    public Clip clip = Clip.Frogs;
    public float range = 30f;

    void Awake()
    {
        var source = GetComponent<AudioSource>();
        source.clip = clip == Clip.Frogs ? SoundBank.Frogs : SoundBank.Trickle;
        source.loop = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = range;
        source.dopplerLevel = 0f;
        source.pitch = Random.Range(0.93f, 1.07f);
        source.time = Random.value * source.clip.length;
        if (GetComponentInParent<TimeWindow>() == null) source.Play();
    }
}
