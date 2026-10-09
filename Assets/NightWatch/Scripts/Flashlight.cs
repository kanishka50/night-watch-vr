using UnityEngine;

// A flashlight for finding elephants in the dark. Grab it and press use to switch it on or off.
// Mainly for seeing. Shone straight at an elephant it also makes it hesitate a little.
public class Flashlight : PlayerAction
{
    public Light beam;
    public float range = 25f;
    [Tooltip("How close to the centre of the beam an elephant must be, in degrees.")]
    public float beamAngle = 12f;
    [Tooltip("Scare strength every half second while the beam is on an elephant.")]
    public float strength = 1f;

    float tick;
    bool logged;

    protected override void Update()
    {
        base.Update();
        if (beam == null || !beam.enabled || !IsHeld) return;
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.5f;

        foreach (var e in ElephantAI.All.ToArray())
        {
            if (!e.IsActive) continue;
            var toElephant = e.transform.position + Vector3.up * 1.5f - beam.transform.position;
            if (toElephant.magnitude > range || Vector3.Angle(beam.transform.forward, toElephant) > beamAngle) continue;
            e.Scare("flashlight", strength, beam.transform.position, ElephantAI.Reaction.Pause);
            if (!logged)
            {
                logged = true;
                ActionLog.Record("Found an elephant with the flashlight", true, "Seeing them early gives you time to act.");
            }
        }
    }

    protected override void Awake()
    {
        base.Awake();
        if (beam != null) beam.enabled = false;
    }

    protected override string Hint => "Flashlight: press Use (trigger / T) to switch it on. Find elephants in the dark.";

    protected override void OnUsed()
    {
        if (beam != null) beam.enabled = !beam.enabled;
    }
}
