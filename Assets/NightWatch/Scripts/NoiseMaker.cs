using UnityEngine;

// Tin cans (or a drum). Grab and press use to bang them. A small scare for elephants nearby.
// Cheap and common, but elephants get used to the same noise quickly.
public class NoiseMaker : PlayerAction
{
    public float range = 25f;
    public float strength = 8f;
    public float cooldown = 1.5f;

    float lastUse = float.NegativeInfinity;
    bool logged;

    protected override string Hint => "Tin cans: press Use (trigger / T) to bang them. Best before elephants reach the field.";

    protected override void OnUsed()
    {
        if (Time.time - lastUse < cooldown) return;
        lastUse = Time.time;
        SoundBank.PlayAt(SoundBank.TinBang, transform.position, 1f);
        int reached = ElephantAI.ScareAllNear(transform.position, range, "tin cans", strength);
        if (!logged && reached > 0)
        {
            logged = true;
            ActionLog.Record("Banged tin cans to warn elephants", true, "Noise warns elephants off.");
        }
    }
}
