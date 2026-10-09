using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Point to learn: when a hand's pointer rests on a LearnSpot (the dagaba, the tank, the sluice,
// the kamatha, a scarecrow, the cattle...) for a moment, one short line fades in low in the view
// and stays a little after the pointer moves on. Its own long ray (the hands' pointers only reach
// 10 m), stopped by anything solid, so the dagaba can be learned from the hut but not through a wall.
public class LearnPointer : MonoBehaviour
{
    public TextMeshPro text;
    public float range = 150f;
    [Tooltip("Seconds the pointer must rest on a spot before its line shows.")]
    public float dwell = 0.35f;
    [Tooltip("Seconds the line stays after the pointer leaves.")]
    public float linger = 2.5f;

    [Header("Test (read only)")]
    public string pointingAt;

    const int SpotLayer = 2;   // Ignore Raycast
    readonly RaycastHit[] hits = new RaycastHit[8];
    readonly List<NearFarInteractor> hands = new List<NearFarInteractor>();
    float nextFind, restTime, lastSeen, shown;
    LearnSpot current, showing;

    void Awake()
    {
        if (text == null) return;
        // Drawn on top of everything, like the tool hints.
        var mat = text.fontMaterial;
        mat.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
        mat.renderQueue = 4001;
        text.alpha = 0f;
    }

    void Update()
    {
        if (Time.time > nextFind)
        {
            nextFind = Time.time + 2f;
            hands.Clear();
            hands.AddRange(FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None));
        }

        LearnSpot spot = null;
        foreach (var hand in hands)
        {
            if (hand == null || !hand.isActiveAndEnabled || hand.hasSelection) continue;
            var origin = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
            spot = Find(new Ray(origin.position, origin.forward));
            if (spot != null) break;
        }
        pointingAt = spot != null ? spot.title : "";

        if (spot != current)
        {
            current = spot;
            restTime = Time.time;
        }
        if (current != null)
        {
            lastSeen = Time.time;
            if (Time.time - restTime >= dwell && current != showing)
            {
                showing = current;
                if (text != null) text.text = $"<b>{current.title}</b>: {current.line}";
            }
        }
        bool want = showing != null && Time.time - lastSeen < linger;
        if (!want && shown <= 0.01f) showing = null;
        shown = Mathf.MoveTowards(shown, want ? 1f : 0f, Time.deltaTime * 4f);
        if (text != null)
        {
            text.alpha = shown;
            text.enabled = shown > 0.01f;
        }
    }

    // The nearest learn spot along the ray, if nothing solid is in front of it.
    LearnSpot Find(Ray ray)
    {
        float block = range;
        if (Physics.Raycast(ray, out var solid, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            block = solid.distance + 0.05f;
        int count = Physics.RaycastNonAlloc(ray, hits, block, 1 << SpotLayer, QueryTriggerInteraction.Collide);
        LearnSpot best = null;
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var s = hits[i].collider.GetComponent<LearnSpot>();
            if (s != null && hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                best = s;
            }
        }
        return best;
    }
}
