using System.Collections.Generic;
using UnityEngine;

// A bonfire at one of the open sides of the field (east or south). Farmers keep
// fires burning at the field edge so elephants coming out of the forest hesitate.
// Point at the woodpile and press G to light it (in the afternoon, from the ground).
// While it burns, every few seconds each elephant in range stops for a moment and loses a
// little resolve. Elephants get used to a fire that never moves (habituation), so it slows
// them down but rarely turns them back alone. It burns out about halfway through the night.
public class Bonfire : PressTarget
{
    public static readonly List<Bonfire> All = new List<Bonfire>();

    [Tooltip("Which side of the field it guards, for messages and the debrief.")]
    public string side = "east";

    [Header("Parts")]
    [Tooltip("The flame object (shape, light and sound), shown only while it burns.")]
    public GameObject flame;
    public Light fireLight;
    public float lightIntensity = 4f;

    [Header("Effect on elephants")]
    public float range = 8f;
    [Tooltip("Scare strength each time (a fire torch is 2 every half second, a firecracker 60).")]
    public float strength = 5f;
    public float interval = 2f;

    [Header("Burning")]
    [Tooltip("Seconds into the night when the fuel runs out.")]
    public float burnsOutAtNightTime = 150f;
    [Tooltip("The flame shrinks over these last seconds.")]
    public float fadeSeconds = 20f;

    public bool IsLit { get; private set; }
    public bool BurntOut { get; private set; }

    float tick;
    bool toldSlowed;
    Vector3 flameScale;
    static bool toldBurningOut;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => toldBurningOut = false;

    public static int LitCount
    {
        get
        {
            int n = 0;
            foreach (var b in All)
                if (b.IsLit || b.BurntOut) n++;
            return n;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        if (flame == null) return;
        flameScale = flame.transform.localScale;
        var crackle = flame.GetComponent<AudioSource>();
        if (crackle != null)
        {
            crackle.clip = SoundBank.FireCrackle;
            crackle.loop = true;
            crackle.playOnAwake = true;
        }
        flame.SetActive(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        All.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        All.Remove(this);
    }

    protected override void OnPressed()
    {
        if (IsLit) NoticeBoard.Message($"The {side} bonfire is already burning.");
        else if (BurntOut) NoticeBoard.Message($"The {side} bonfire has burnt out. There is no wood left.");
        else LightFire();
    }

    public void LightFire()
    {
        if (IsLit || BurntOut) return;
        IsLit = true;
        tick = interval;
        if (flame != null) flame.SetActive(true);
        SoundBank.PlayAt(SoundBank.FireCrackle, transform.position, 1f);
        NoticeBoard.Message($"The {side} bonfire is burning.");
    }

    protected override void Update()
    {
        base.Update();
        if (!IsLit) return;

        float size = FuelLeft();
        if (size <= 0f)
        {
            GoOut();
            return;
        }
        if (flame != null) flame.transform.localScale = flameScale * Mathf.Lerp(0.3f, 1f, size);
        if (fireLight != null)
            fireLight.intensity = lightIntensity * size * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 5f, transform.position.x));

        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = interval;
        // The fire reaches less far as it burns down.
        int reached = ElephantAI.ScareAllNear(transform.position, range * Mathf.Lerp(0.5f, 1f, size), "bonfire", strength);
        if (reached > 0 && !toldSlowed)
        {
            toldSlowed = true;
            NoticeBoard.Message($"The {side} bonfire made an elephant stop and hesitate.");
        }
    }

    // 1 while there is plenty of wood, down to 0 when it runs out in the night.
    float FuelLeft()
    {
        var game = GameManager.Instance;
        if (game == null) return 1f;
        switch (game.CurrentPhase)
        {
            case GameManager.Phase.Afternoon:
            case GameManager.Phase.Dusk:
                return 1f;
            case GameManager.Phase.Night:
                return Mathf.Clamp01((burnsOutAtNightTime - game.PhaseTime) / fadeSeconds);
            default:
                return 0f;
        }
    }

    void GoOut()
    {
        IsLit = false;
        BurntOut = true;
        if (flame != null) flame.SetActive(false);
        var game = GameManager.Instance;
        if (!toldBurningOut && game != null && game.CurrentPhase == GameManager.Phase.Night)
        {
            toldBurningOut = true;
            NoticeBoard.Message("The bonfires have burnt out. Now only your tools keep the elephants back.");
        }
    }

    [ContextMenu("Test: light this bonfire")]
    void TestLight() => LightFire();
}
