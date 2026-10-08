using UnityEngine;

// A fire torch (pandama). Light it at the fire pot, then hold it out toward an elephant.
// It holds the line: while it burns and is held, every elephant in front of the player within
// range stops (no walking, no eating) and its resolve drains slowly until it gives up.
// Gentler than a firecracker, but unlimited: it burns out after a while and can be relit.
public class FireTorch : PlayerAction, IIgnitable
{
    [Tooltip("The flame object (shape, light and sound), shown only while lit.")]
    public GameObject flame;
    public Light flameLight;
    public float flameIntensity = 6f;
    public float burnSeconds = 45f;
    public float range = 16f;
    [Tooltip("Elephants within this angle of where the player looks are affected.")]
    public float angle = 70f;
    [Tooltip("Scare strength every half second.")]
    public float strength = 2f;

    public bool IsLit { get; private set; }

    float burnLeft;
    float tick;
    bool loggedLight;
    bool loggedScare;
    float messageTimer;

    protected override void Awake()
    {
        base.Awake();
        if (flame == null) return;
        // The crackle plays whenever the flame is shown.
        var crackle = flame.GetComponent<AudioSource>();
        if (crackle != null)
        {
            crackle.clip = SoundBank.FireCrackle;
            crackle.loop = true;
            crackle.playOnAwake = true;
        }
        flame.SetActive(false);
    }

    // Use (trigger) near the fire pot lights it. Touching the pot with the torch also works.
    protected override void OnUsed()
    {
        if (IsLit) return;
        if (FirePot.IsNear(transform.position)) Ignite();
        else FirePot.TooFarMessage();
    }

    public void Ignite()
    {
        if (IsLit) return;
        IsLit = true;
        burnLeft = burnSeconds;
        if (flame != null) flame.SetActive(true);
        NoticeBoard.Message("The torch is lit. Hold it toward an elephant to stop it.");
        if (!loggedLight)
        {
            loggedLight = true;
            ActionLog.Record("Lit a fire torch", true, "Fire held by a person is one of the most common ways farmers keep elephants back.");
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!IsLit) return;

        burnLeft -= Time.deltaTime;
        if (flameLight != null) flameLight.intensity = flameIntensity * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 8f, 1f));
        if (burnLeft <= 0f)
        {
            IsLit = false;
            if (flame != null) flame.SetActive(false);
            NoticeBoard.Message("Your fire torch has burnt out. Relight it at the fire pot.");
            return;
        }

        if (!IsHeld) return;
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.5f;

        var head = PlayerState.Head;
        if (head == null) return;
        foreach (var e in ElephantAI.All.ToArray())
        {
            if (!e.IsActive) continue;
            var toElephant = e.transform.position - head.position;
            toElephant.y = 0f;
            var look = head.forward;
            look.y = 0f;
            if (toElephant.magnitude > range || Vector3.Angle(look, toElephant) > angle) continue;
            e.Scare("fire torch", strength, transform.position, ElephantAI.Reaction.Pause);
            if (Time.time > messageTimer)
            {
                messageTimer = Time.time + 12f;
                NoticeBoard.Message("The elephant has stopped. Keep the fire on it.");
            }
            if (!loggedScare)
            {
                loggedScare = true;
                ActionLog.Record("Held a fire torch toward an elephant", true, "The elephant stopped. Fire from a safe place makes elephants hesitate and turn back.");
            }
        }
    }
}
