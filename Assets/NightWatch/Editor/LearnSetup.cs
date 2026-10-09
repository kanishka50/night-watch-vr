using UnityEditor;
using UnityEngine;

// Point to learn (living-village.md, block E): invisible spots on the village's things, each with
// one short fact, and the line low in the view that shows it (LearnPointer). The builders of each
// thing call Spot (canal, kamatha, scarecrows, cattle, egrets...); GameSetup calls BuildLine.
// Facts on screen stay short and plain. Any fact used on a slide needs a source first.
public static class LearnSetup
{
    public const int SpotLayer = 2;   // Ignore Raycast: unseen by XRI pointers and by the elephants

    // The facts, by key, so the wording lives in one place.
    public static readonly (string key, string title, string line)[] Facts =
    {
        ("wewa", "Wewa (village tank)", "rain is stored here to water the paddy through the dry season."),
        ("sorowwa", "Sorowwa (sluice)", "lift the wooden gate and tank water flows out under the bund."),
        ("outlet", "Sluice outlet", "the tank's water comes out of the bund here and runs into the ela."),
        ("ela", "Ela (canal)", "carries tank water down to the paddy, plot after plot."),
        ("kamatha", "Kamatha (threshing floor)", "cattle once trod the cut paddy here to free the grain."),
        ("cattle", "Village cattle", "tied near home at night. Bulls once ploughed and threshed the paddy."),
        ("pambaya", "Pambaya (scarecrow)", "a clay-pot head and an old shirt. Its tin can rattles to scare birds."),
        ("agrowell", "Agro-well", "a dug well that waters crops when the tank runs low."),
        ("dagaba", "Dagaba", "a Buddhist shrine. At dusk villagers light small clay oil lamps (pahan) here."),
        ("kokka", "Kokka (egret)", "hunts frogs and insects in the flooded paddy. Flies to its roost at dusk."),
    };

    // A round spot (centre in world space), or a box (size in the parent's turn).
    public static GameObject Spot(Transform parent, string key, Vector3 centre, float radius) =>
        Make(parent, key, centre, Quaternion.identity, Vector3.one * radius * 2f, true);

    public static GameObject Spot(Transform parent, string key, Vector3 centre, Quaternion turn, Vector3 size) =>
        Make(parent, key, centre, turn, size, false);

    static GameObject Make(Transform parent, string key, Vector3 centre, Quaternion turn, Vector3 size, bool round)
    {
        string title = key, line = "";
        foreach (var f in Facts)
            if (f.key == key) { title = f.title; line = f.line; }
        var go = new GameObject("Learn_" + key);
        go.layer = SpotLayer;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(centre, turn);
        if (round)
        {
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = size.x / 2f / Mathf.Max(go.transform.lossyScale.x, 1e-4f);
        }
        else
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var s = go.transform.lossyScale;
            box.size = new Vector3(size.x / Mathf.Max(s.x, 1e-4f), size.y / Mathf.Max(s.y, 1e-4f), size.z / Mathf.Max(s.z, 1e-4f));
        }
        var spot = go.AddComponent<LearnSpot>();
        spot.title = title;
        spot.line = line;
        return go;
    }

    // The fact line: on the camera, below the tool hint (EndingSetup.BuildHint), with an outline.
    public static void BuildLine()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var root = EndingSetup.FollowPanel("LearnLine", cam.transform, new Vector3(0f, -0.3f, 0.9f));
        var text = EditorKit.Text("Text", root.transform, Vector3.zero, Quaternion.identity, new Vector2(0.6f, 0.08f), "", 0.26f,
            TMPro.TextAlignmentOptions.Center);
        text.transform.localPosition = Vector3.zero;
        text.transform.localRotation = Quaternion.identity;
        text.fontSharedMaterial = EndingSetup.OutlineMaterial(text.fontSharedMaterial);
        root.AddComponent<LearnPointer>().text = text;
    }
}
