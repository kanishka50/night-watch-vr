using UnityEngine;

// The sound of running water at one place (the canal's outlet and its end in the field): a loop
// of trickling and small bubbles, heard only near it (3D, fades out by about 18 m).
[RequireComponent(typeof(AudioSource))]
public class WaterSound : MonoBehaviour
{
    void Start()
    {
        var source = GetComponent<AudioSource>();
        source.clip = SoundBank.Trickle;
        source.loop = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = 18f;
        source.dopplerLevel = 0f;
        // Two places never play the loop in step.
        source.time = Random.value * source.clip.length;
        source.pitch = Random.Range(0.92f, 1.08f);
        source.Play();
    }
}
