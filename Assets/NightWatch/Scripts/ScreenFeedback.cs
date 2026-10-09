using System.Collections;
using UnityEngine;

// Effects on the player's view, using a see-through panel just in front of the VR camera:
//  - Flash: a short red (or other colour) flash when the player is hurt
//  - FadeOut / FadeIn: to and from black, for endings
//  - Blink: a short fade to black and back when the view jumps (teleport, ladder, story scenes).
//    Hiding the jump keeps players from feeling sick (course week 5: comfort). It replaced the
//    round tunnel vignette, which closed to a small circle and looked odd.
//  - KnockDown: the view tips over and drops, as if the player fell. If fatal, it then fades out.
//  - Wound edge: a red edge round the view when the player is hit, fading back to normal in
//    about 4 s. Only when badly hurt (one more hit could kill) a thin, faint edge stays, pulsing.
public class ScreenFeedback : MonoBehaviour
{
    public static ScreenFeedback Instance { get; private set; }

    [Tooltip("The see-through panel in front of the camera.")]
    public Renderer overlay;
    [Tooltip("A second panel, just behind the first, for the red wound edge.")]
    public Renderer woundOverlay;

    MaterialPropertyBlock block;
    MaterialPropertyBlock woundBlock;
    float woundAlpha;
    float hitGlow;
    float lastHealth = 100f;

    [Tooltip("At or below this health a faint red edge stays as a warning.")]
    public float lowHealth = 40f;
    Color flashColor = Color.red;
    float flashAlpha;
    float fadeAlpha;
    float blinkAlpha;
    Coroutine fade;
    Coroutine blink;

    // Blink timing in seconds: to black, and back from black.
    public const float BlinkOutSeconds = 0.15f, BlinkInSeconds = 0.35f;

    void Awake()
    {
        Instance = this;
        block = new MaterialPropertyBlock();
        Apply();

        if (woundOverlay != null)
        {
            woundBlock = new MaterialPropertyBlock();
            woundBlock.SetTexture("_BaseMap", EdgeTexture());
            woundOverlay.enabled = false;
        }
    }

    void Update()
    {
        UpdateWound();

        if (flashAlpha > 0f)
        {
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.deltaTime * 1.2f);
            Apply();
        }
    }

    public static void Flash(Color color, float strength = 0.5f)
    {
        if (Instance == null) return;
        Instance.flashColor = color;
        Instance.flashAlpha = Mathf.Max(Instance.flashAlpha, strength);
        Instance.Apply();
    }

    // The blink: to black (before a jump) and back (after it). Works on its own value, so it never
    // spoils an ending's fade.
    public static void BlinkOut(float seconds = BlinkOutSeconds) => Instance?.StartBlink(1f, seconds);
    public static void BlinkIn(float seconds = BlinkInSeconds) => Instance?.StartBlink(0f, seconds);
    // Part way to black (a snap turn only dims the view).
    public static void BlinkTo(float darkness, float seconds) => Instance?.StartBlink(Mathf.Clamp01(darkness), seconds);

    // Black at once, then back: for a jump that cannot wait (the very first placement).
    public static void BlinkNow()
    {
        if (Instance == null) return;
        Instance.blinkAlpha = 1f;
        Instance.Apply();
        BlinkIn();
    }

    void StartBlink(float target, float seconds)
    {
        if (blink != null) StopCoroutine(blink);
        blink = StartCoroutine(BlinkRoutine(target, seconds));
    }

    IEnumerator BlinkRoutine(float target, float seconds)
    {
        float start = blinkAlpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            blinkAlpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds));
            Apply();
            yield return null;
        }
        blinkAlpha = target;
        Apply();
    }

    public static void FadeOut(float seconds) => Instance?.StartFade(1f, seconds);
    public static void FadeIn(float seconds) => Instance?.StartFade(0f, seconds);

    // Clears any flash that is still fading (used when the night ends).
    public static void ClearFlash()
    {
        if (Instance == null) return;
        Instance.flashAlpha = 0f;
        Instance.Apply();
    }

    public static void KnockDown(bool fatal)
    {
        if (Instance != null) Instance.StartCoroutine(Instance.Fall(fatal));
    }

    void StartFade(float target, float seconds)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(target, seconds));
    }

    IEnumerator FadeRoutine(float target, float seconds)
    {
        float start = fadeAlpha;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            fadeAlpha = Mathf.Lerp(start, target, t / seconds);
            Apply();
            yield return null;
        }
        fadeAlpha = target;
        Apply();
    }

    // Tips the view sideways and down (the camera's parent, so head tracking still works).
    IEnumerator Fall(bool fatal)
    {
        var cam = PlayerState.Head;
        var offset = cam != null ? cam.parent : null;
        if (offset == null) yield break;

        var startRot = offset.localRotation;
        var startPos = offset.localPosition;
        var downRot = startRot * Quaternion.Euler(0f, 0f, 65f);
        var downPos = startPos + new Vector3(0f, -1.1f, 0f);

        Flash(Color.red, 0.7f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            offset.localRotation = Quaternion.Slerp(startRot, downRot, k);
            offset.localPosition = Vector3.Lerp(startPos, downPos, k);
            yield return null;
        }

        if (fatal)
        {
            StartFade(1f, 2f);
            yield return new WaitForSeconds(2.5f);
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        // Stand back up (after a fatal fall this happens behind the black screen).
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
            offset.localRotation = Quaternion.Slerp(downRot, startRot, k);
            offset.localPosition = Vector3.Lerp(downPos, startPos, k);
            yield return null;
        }
        offset.localRotation = startRot;
        offset.localPosition = startPos;
    }

    // A hit: a strong red edge that fades in about 4 s. Badly hurt: a faint pulsing edge stays.
    void UpdateWound()
    {
        if (woundOverlay == null) return;
        if (woundBlock == null)
        {
            // Rebuilt if lost (for example after Unity reloads scripts during Play).
            woundBlock = new MaterialPropertyBlock();
            woundBlock.SetTexture("_BaseMap", EdgeTexture());
        }

        // Size the edge panel to exactly fill the camera's view, so the red edge is at the edges.
        var cam = GetComponentInParent<Camera>();
        if (cam != null)
        {
            float distance = woundOverlay.transform.localPosition.z;
            float height = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            woundOverlay.transform.localScale = new Vector3(height * cam.aspect, height, 1f);
        }

        var game = GameManager.Instance;
        float health = game != null ? game.peopleSafe : 100f;
        if (health < lastHealth) hitGlow = 0.7f;
        lastHealth = health;
        hitGlow = Mathf.MoveTowards(hitGlow, 0f, Time.deltaTime * 0.7f / 4f);
        float warning = health <= lowHealth ? 0.22f : 0f;
        float target = Mathf.Max(hitGlow, warning);
        // The red edge is for the danger of the night. It fades at dawn and is gone at the end.
        var phase = game != null ? game.CurrentPhase : GameManager.Phase.Night;
        if (phase == GameManager.Phase.Dawn || phase == GameManager.Phase.Debrief) target = 0f;
        if (phase == GameManager.Phase.Debrief) woundAlpha = 0f;   // gone at once, and stays gone
        // Comes in fast with a hit, goes out slowly.
        woundAlpha = Mathf.MoveTowards(woundAlpha, target, Time.deltaTime * (target > woundAlpha ? 3f : 0.5f));
        float pulse = hitGlow > warning ? 1f : 0.8f + 0.2f * Mathf.Sin(Time.time * 3f);
        woundBlock.SetColor("_BaseColor", new Color(0.7f, 0f, 0f, woundAlpha * pulse));
        woundOverlay.SetPropertyBlock(woundBlock);
        woundOverlay.enabled = woundAlpha > 0.01f;
    }

    // A square picture: see-through over most of the middle, solid only near the edges.
    static Texture2D EdgeTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1.2f, d));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }

    void Apply()
    {
        if (overlay == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        // The coloured flash with the black fade on top of it. The flash only adds colour while it is
        // showing, so a fade to or from black is always pure black (never a red wash).
        float black = Mathf.Max(fadeAlpha, blinkAlpha);
        float a = 1f - (1f - black) * (1f - flashAlpha);
        var c = a > 0.001f ? flashColor * (flashAlpha * (1f - black) / a) : Color.black;
        c.a = Mathf.Clamp01(a);
        block.SetColor("_BaseColor", c);
        overlay.SetPropertyBlock(block);
        overlay.enabled = c.a > 0.001f;
    }
}
