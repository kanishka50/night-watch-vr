using System.Collections;
using TMPro;
using UnityEngine;

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
    // While the card shows, the camera draws only the card's layer on black: nothing in the world
    // (hands, rays, elephants, overlays) can cover or cut the message.
    Camera shownBy;
    int savedMask;
    CameraClearFlags savedClear;
    Color savedBackground;

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
        instance.ShowOnlyCard();
        instance.StopSound();
        instance.soundRoutine = instance.StartCoroutine(instance.PlaySound(good));
    }

    public static void Hide()
    {
        if (instance == null || instance.card == null) return;
        instance.card.SetActive(false);
        instance.ShowWorld();
        instance.StopSound();
    }

    void ShowOnlyCard()
    {
        if (shownBy != null) return;
        var cam = Camera.main;
        if (cam == null || card == null) return;
        shownBy = cam;
        savedMask = cam.cullingMask;
        savedClear = cam.clearFlags;
        savedBackground = cam.backgroundColor;
        cam.cullingMask = 1 << card.layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    void ShowWorld()
    {
        if (shownBy == null) return;
        shownBy.cullingMask = savedMask;
        shownBy.clearFlags = savedClear;
        shownBy.backgroundColor = savedBackground;
        shownBy = null;
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
