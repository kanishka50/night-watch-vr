using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

// The ending card: a big title and one short line that appear in front of the player's eyes on
// the black screen at the end of the night, with a sound (birds for a good ending, a slow low
// drum for a bad one). Then the results board is shown. Sits on the camera, so it follows the head.
public class EndingCard : MonoBehaviour
{
    static EndingCard instance;

    public GameObject card;
    public TextMeshPro title;
    public TextMeshPro line;
    public AudioSource sound;
    public Color goodColor = new Color(0.61f, 1f, 0.54f);
    public Color badColor = new Color(1f, 0.70f, 0.42f);

    Coroutine soundRoutine;
    // The controller rays, hidden while the card shows (they drew over the message).
    readonly List<Behaviour> hiddenRayScripts = new List<Behaviour>();
    readonly List<LineRenderer> hiddenRays = new List<LineRenderer>();

    void Awake()
    {
        instance = this;
        // Draw on top of the black fade panel and of anything in the world.
        foreach (var text in new[] { title, line })
        {
            if (text == null) continue;
            var mat = text.fontMaterial;
            mat.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4001;
        }
        if (card != null) card.SetActive(false);
    }

    public static void Show(string titleText, string lineText, bool good)
    {
        if (instance == null || instance.card == null) return;
        instance.title.text = titleText;
        instance.title.color = good ? instance.goodColor : instance.badColor;
        instance.line.text = lineText;
        instance.card.SetActive(true);
        instance.HideRays();
        instance.StopSound();
        instance.soundRoutine = instance.StartCoroutine(instance.PlaySound(good));
    }

    public static void Hide()
    {
        if (instance == null || instance.card == null) return;
        instance.card.SetActive(false);
        instance.ShowRays();
        instance.StopSound();
    }

    // The ray scripts switch their lines back on every frame, so they are paused too.
    void HideRays()
    {
        if (hiddenRays.Count > 0 || hiddenRayScripts.Count > 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        var rig = cam.transform.root;
        foreach (var script in rig.GetComponentsInChildren<Behaviour>())
        {
            if (!(script is CurveVisualController) && !(script is XRInteractorLineVisual)) continue;
            if (!script.enabled) continue;
            script.enabled = false;
            hiddenRayScripts.Add(script);
        }
        foreach (var line in rig.GetComponentsInChildren<LineRenderer>())
        {
            if (!line.enabled) continue;
            line.enabled = false;
            hiddenRays.Add(line);
        }
    }

    void ShowRays()
    {
        foreach (var line in hiddenRays) if (line != null) line.enabled = true;
        foreach (var script in hiddenRayScripts) if (script != null) script.enabled = true;
        hiddenRays.Clear();
        hiddenRayScripts.Clear();
    }

    void StopSound()
    {
        if (soundRoutine != null) StopCoroutine(soundRoutine);
        soundRoutine = null;
        if (sound != null)
        {
            sound.Stop();
            sound.pitch = 1f;
        }
    }

    IEnumerator PlaySound(bool good)
    {
        if (sound == null) yield break;
        sound.spatialBlend = 0f;
        if (good)
        {
            sound.clip = SoundBank.Birds;
            sound.loop = true;
            sound.volume = 0.6f;
            sound.Play();
            yield break;
        }
        // Three slow, heavy beats.
        sound.loop = false;
        for (int i = 0; i < 3; i++)
        {
            sound.pitch = i == 2 ? 0.8f : 0.9f;
            sound.PlayOneShot(SoundBank.Drum, 0.8f);
            yield return new WaitForSecondsRealtime(1.1f);
        }
        sound.pitch = 1f;
    }

    [ContextMenu("Test: show a good ending card")]
    void TestGood() => StartCoroutine(TestCard(true));

    [ContextMenu("Test: show a bad ending card")]
    void TestBad() => StartCoroutine(TestCard(false));

    IEnumerator TestCard(bool good)
    {
        ScreenFeedback.FadeOut(1f);
        yield return new WaitForSecondsRealtime(1f);
        if (good) Show("Everyone is safe", "The herd went back to the forest. You, your neighbour and the elephants are all alive.", true);
        else Show("You did not survive the night", "An elephant reached you. No one can outrun an elephant in the dark.", false);
        yield return new WaitForSecondsRealtime(4f);
        Hide();
        ScreenFeedback.FadeIn(1f);
    }
}
