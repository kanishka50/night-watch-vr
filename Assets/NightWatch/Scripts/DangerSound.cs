using UnityEngine;

// A heartbeat that grows louder as an active elephant gets close to the player.
// Much louder on the ground, where the player is in real danger.
[RequireComponent(typeof(AudioSource))]
public class DangerSound : MonoBehaviour
{
    public float range = 14f;
    public float hutVolume = 0.3f;
    public float groundVolume = 0.9f;

    AudioSource source;

    void Start()
    {
        source = GetComponent<AudioSource>();
        source.clip = SoundBank.Heartbeat;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.Play();
    }

    void Update()
    {
        float nearest = float.MaxValue;
        foreach (var e in ElephantAI.All)
        {
            if (!e.IsActive) continue;
            nearest = Mathf.Min(nearest, PlayerState.FlatDistanceTo(e.transform.position));
        }

        float closeness = nearest < range ? 1f - nearest / range : 0f;
        float target = closeness * (PlayerState.OnGround ? groundVolume : hutVolume);
        source.volume = Mathf.MoveTowards(source.volume, target, Time.deltaTime);
        source.pitch = 1f + closeness * 0.4f;   // faster when closer
    }
}
