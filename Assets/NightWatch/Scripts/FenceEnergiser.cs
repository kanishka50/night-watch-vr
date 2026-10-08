using UnityEngine;

// The fence energiser: the box that sends pulses of power down the electric fence.
// Farmers switch it on in the evening and off in the morning, so people and cattle are safe by day.
// Point at it and press G to switch it on or off. When it is on, a red lamp shows and it ticks
// once a second. When it is off, the fence is only wire: elephants push straight through it.
public class FenceEnergiser : PressTarget
{
    public static FenceEnergiser Instance { get; private set; }

    // True when the fence has power. An old scene with no energiser counts as always on.
    public static bool IsOn => Instance == null || Instance.on;

    public bool on;
    [Tooltip("The small lamp on the front of the box.")]
    public Renderer lamp;
    public Color lampOnColor = new Color(1f, 0.15f, 0.1f);
    public Color lampOffColor = new Color(0.2f, 0.05f, 0.05f);
    public AudioSource tickSource;
    public float tickInterval = 1.2f;

    float tickTimer;
    float lampFlash;
    MaterialPropertyBlock lampColor;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        ShowLamp(false);
    }

    protected override void OnPressed() => Switch(!on);

    public void Switch(bool value)
    {
        on = value;
        ShowLamp(false);
        SoundBank.PlayAt(SoundBank.Tick, transform.position, 1f, 1f);
        NoticeBoard.Message(on
            ? "The fence energiser is on. The fence is live."
            : "The fence energiser is off! The fence will not stop elephants.");
    }

    protected override void Update()
    {
        base.Update();
        if (!on || tickSource == null) return;
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0f) return;
        tickTimer = tickInterval;
        tickSource.PlayOneShot(SoundBank.Tick);
        // The lamp flashes with each pulse.
        lampFlash = 0.15f;
        ShowLamp(true);
    }

    void LateUpdate()
    {
        if (lampFlash <= 0f) return;
        lampFlash -= Time.deltaTime;
        if (lampFlash <= 0f) ShowLamp(false);
    }

    void ShowLamp(bool bright)
    {
        if (lamp == null) return;
        if (lampColor == null) lampColor = new MaterialPropertyBlock();
        var c = on ? lampOnColor : lampOffColor;
        if (on && !bright) c *= 0.6f;
        lampColor.SetColor("_BaseColor", c);
        lamp.SetPropertyBlock(lampColor);
    }

    [ContextMenu("Test: switch the energiser on or off")]
    void TestSwitch() => Switch(!on);
}
