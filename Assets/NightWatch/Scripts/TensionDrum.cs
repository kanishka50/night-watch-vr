using UnityEngine;

// Background music for danger: a low drum that starts when an elephant comes near the hut and
// gets faster and louder as the danger grows. It is a soundtrack only, not a sound in the world
// (the heartbeat is the player's own fear, the drum is the scene's tension).
//   level 1  an elephant within 25 m of the hut (in the field or at the fence): slow beat
//   level 2  an elephant within 12 m, or an agitated one: faster
//   level 3  an elephant charging: fast, loud, double beat
[RequireComponent(typeof(AudioSource))]
public class TensionDrum : MonoBehaviour
{
    public float nearRange = 25f;
    public float closeRange = 12f;
    [Tooltip("Seconds between beats for levels 1, 2 and 3.")]
    public float[] beatInterval = { 1.3f, 0.8f, 0.45f };
    [Tooltip("Volume for levels 1, 2 and 3.")]
    public float[] levelVolume = { 0.25f, 0.4f, 0.6f };
    [Tooltip("How fast the volume follows the level (per second).")]
    public float fadeSpeed = 0.5f;

    AudioSource source;
    float volume;
    float beatTimer;
    int beatCount;
    int level;
    float checkTimer;

    void Start()
    {
        source = GetComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.loop = false;
        source.playOnAwake = false;
    }

    void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.25f;
            level = DangerLevel();
        }

        float target = level > 0 ? levelVolume[level - 1] : 0f;
        volume = Mathf.MoveTowards(volume, target, fadeSpeed * Time.deltaTime);
        if (volume <= 0.01f)
        {
            beatTimer = 0f;
            return;
        }

        beatTimer -= Time.deltaTime;
        if (beatTimer > 0f) return;
        int index = Mathf.Max(level, 1) - 1;
        beatTimer = beatInterval[index];
        beatCount++;

        // "Dum, dum, dum, ta": every fourth beat is higher. At level 3 a quick second hit.
        source.pitch = beatCount % 4 == 0 ? 1.5f : 1f;
        source.PlayOneShot(SoundBank.Drum, volume);
        if (level == 3) Invoke(nameof(SecondHit), 0.15f);
    }

    void SecondHit()
    {
        source.pitch = 1.2f;
        source.PlayOneShot(SoundBank.Drum, volume * 0.7f);
    }

    int DangerLevel()
    {
        var game = GameManager.Instance;
        if (game != null && game.CurrentPhase == GameManager.Phase.Debrief) return 0;

        int result = 0;
        foreach (var e in ElephantAI.All)
        {
            if (!e.IsActive) continue;
            if (e.CurrentState == ElephantAI.State.Charge) return 3;
            float d = Flat(e.transform.position - ElephantAI.HutCenter);
            if (e.CurrentState == ElephantAI.State.Agitated || d < closeRange) result = Mathf.Max(result, 2);
            else if (d < nearRange) result = Mathf.Max(result, 1);
        }
        return result;
    }

    static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }
}
