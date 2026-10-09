using System;
using System.Collections;
using TMPro;
using UnityEngine;

// The story at the very beginning, before the START screen, told on an ola leaf (puskola, the
// palm-leaf books of old Sri Lanka). Five short scenes. For each one the view blinks dark and
// the player stands at a new spot in the village, free to look around (the camera never moves
// by itself: that would cause motion sickness, course week 5), and the leaf shows one line.
// In the jungle scene an elephant walks past in the distance. Then the player is back at the
// start spot and the START screen appears. "Skip story" on the leaf ends it at once.
public class StoryIntro : MonoBehaviour
{
    [Serializable]
    public class Scene
    {
        public Transform spot;
        [TextArea] public string text;
        public float seconds = 8f;
        [Tooltip("An elephant walks past in the distance during this scene.")]
        public bool elephant;
    }

    [Tooltip("Off = straight to the START screen (handy while testing other things).")]
    public bool play = true;
    public Scene[] scenes;

    [Header("Parts")]
    [Tooltip("The ola leaf with the text and the skip button. Placed in front of each spot.")]
    public Transform leaf;
    public TextMeshPro leafText;
    public Transform startSpot;
    [Tooltip("The elephant model to copy for the walking elephant (the template's Model).")]
    public GameObject elephantModel;
    [Tooltip("Where the walking elephant starts and ends (heights are set on the ground).")]
    public Vector3 elephantFrom, elephantTo;

    [Header("Look")]
    [Tooltip("The leaf stands this far in front of the player, at this height.")]
    public float leafDistance = 1.8f;
    public float leafHeight = 1.45f;
    public float textFadeSeconds = 0.8f;

    public bool IsPlaying { get; private set; }

    bool skip;
    AudioSource evening;
    GameObject walker;

    void Start()
    {
        if (leaf != null) leaf.gameObject.SetActive(false);
        if (!play || scenes == null || scenes.Length == 0) return;
        StartCoroutine(Tell());
    }

    // Called by the "Skip story" button.
    public void Skip() => skip = true;

    IEnumerator Tell()
    {
        IsPlaying = true;
        // GroundPrep puts the player at the start spot in its Start; wait for that first.
        yield return null;
        ShowStartScreen(false);

        evening = gameObject.AddComponent<AudioSource>();
        evening.clip = SoundBank.Birds;
        evening.loop = true;
        evening.spatialBlend = 0f;
        evening.volume = 0.25f;
        evening.Play();

        foreach (var scene in scenes)
        {
            if (skip) break;
            yield return Show(scene);
        }
        Finish();
    }

    IEnumerator Show(Scene scene)
    {
        var limit = PlayAreaLimit.Instance;
        if (scene.spot != null && limit != null)
        {
            // Fade out, jump to the new spot in the dark, fade back in.
            limit.MoveHeadTo(scene.spot.position, scene.spot.rotation);
            while (limit.IsMoving) yield return null;
        }
        PlaceLeaf(scene.spot);
        if (scene.elephant) StartWalker();

        // The ink appears slowly, stays, then fades before the next scene.
        leafText.text = scene.text;
        for (float t = 0f; t < scene.seconds && !skip; t += Time.deltaTime)
        {
            float inAlpha = Mathf.Clamp01(t / textFadeSeconds);
            float outAlpha = Mathf.Clamp01((scene.seconds - t) / textFadeSeconds);
            leafText.alpha = Mathf.Min(inAlpha, outAlpha);
            yield return null;
        }
        StopWalker();
    }

    void PlaceLeaf(Transform spot)
    {
        if (leaf == null || spot == null) return;
        var forward = spot.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        leaf.SetPositionAndRotation(spot.position + forward * leafDistance + Vector3.up * leafHeight,
            Quaternion.LookRotation(forward));
        leaf.gameObject.SetActive(true);
    }

    // A copy of the elephant model walking across the view (only a picture: no AI, no NavMesh).
    void StartWalker()
    {
        if (elephantModel == null) return;
        walker = Instantiate(elephantModel);
        walker.name = "StoryElephant";
        walker.SetActive(true);
        var animator = walker.GetComponentInChildren<Animator>();
        if (animator != null) animator.Play("Walk", 0, 0f);
        StartCoroutine(Walk());
        SoundBank.PlayAt(SoundBank.Rumble, elephantFrom, 1f, 10f);
    }

    IEnumerator Walk()
    {
        var direction = elephantTo - elephantFrom;
        direction.y = 0f;
        var rotation = Quaternion.LookRotation(direction);
        const float speed = 1.7f;   // the Walk clip's own pace at this size (ElephantBody)
        float length = direction.magnitude;
        for (float d = 0f; walker != null && d < length; d += speed * Time.deltaTime)
        {
            var p = elephantFrom + direction.normalized * d;
            var terrain = Terrain.activeTerrain;
            if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            walker.transform.SetPositionAndRotation(p, rotation);
            yield return null;
        }
    }

    void StopWalker()
    {
        if (walker != null) Destroy(walker);
        walker = null;
    }

    void Finish()
    {
        StopWalker();
        if (leaf != null) leaf.gameObject.SetActive(false);
        if (evening != null) Destroy(evening);
        if (startSpot != null && PlayAreaLimit.Instance != null)
            PlayAreaLimit.Instance.MoveHeadTo(startSpot.position, startSpot.rotation);
        ShowStartScreen(true);
        IsPlaying = false;
    }

    static void ShowStartScreen(bool show)
    {
        var screen = StartScreen.Instance;
        if (screen != null && screen.panel != null) screen.panel.SetActive(show);
    }

    [ContextMenu("Test: skip the story")]
    void TestSkip() => Skip();
}
