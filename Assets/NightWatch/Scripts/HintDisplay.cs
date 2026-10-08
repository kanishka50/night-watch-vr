using TMPro;
using UnityEngine;

// A short hint low in the player's view for a few seconds, then gone. Used once per tool, the
// first time it is grabbed, so the player learns each tool at the moment they need it.
// Sits on the camera, so it follows the head; drawn on top of everything so it is never hidden.
public class HintDisplay : MonoBehaviour
{
    static HintDisplay instance;

    public TextMeshPro text;
    public float seconds = 5f;

    float timer;

    void Awake()
    {
        instance = this;
        if (text == null) return;
        var mat = text.fontMaterial;
        mat.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
        mat.renderQueue = 4001;
        text.text = "";
    }

    public static void Show(string message)
    {
        if (instance == null || instance.text == null) return;
        instance.text.text = message;
        instance.timer = instance.seconds;
    }

    void Update()
    {
        if (timer <= 0f) return;
        timer -= Time.deltaTime;
        if (timer <= 0f && text != null) text.text = "";
    }

    [ContextMenu("Test: show a hint")]
    void TestHint() => Show("Firecracker: press T at the fire pot to light it, T again to throw it. Throw it NEAR elephants.");
}
