using UnityEngine;

// Background sound that is the same everywhere (2D): crickets that start at dusk,
// are loudest at night and fade at dawn, and birds that start singing at dawn.
[RequireComponent(typeof(AudioSource))]
public class AmbientSound : MonoBehaviour
{
    public float nightVolume = 0.35f;

    public float dawnBirdsVolume = 0.35f;

    AudioSource source;
    AudioSource birds;

    void Start()
    {
        source = GetComponent<AudioSource>();
        source.clip = SoundBank.Crickets;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.Play();

        birds = gameObject.AddComponent<AudioSource>();
        birds.clip = SoundBank.Birds;
        birds.loop = true;
        birds.spatialBlend = 0f;
        birds.volume = 0f;
        birds.Play();
    }

    void Update()
    {
        var game = GameManager.Instance;
        if (game == null) return;

        float target;
        switch (game.CurrentPhase)
        {
            case GameManager.Phase.Afternoon: target = 0f; break;
            case GameManager.Phase.Dusk: target = nightVolume * (game.PhaseTime / game.duskLength); break;
            case GameManager.Phase.Night: target = nightVolume; break;
            case GameManager.Phase.Dawn: target = nightVolume * 0.4f; break;
            default: target = nightVolume * 0.2f; break;
        }
        source.volume = Mathf.MoveTowards(source.volume, target, Time.deltaTime * 0.2f);

        bool morning = game.CurrentPhase == GameManager.Phase.Dawn || game.CurrentPhase == GameManager.Phase.Debrief;
        birds.volume = Mathf.MoveTowards(birds.volume, morning ? dawnBirdsVolume : 0f, Time.deltaTime * 0.1f);
    }
}
