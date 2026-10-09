using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the story at the beginning (called by Night Watch > Build Full Game, after the start
// screen and the elephants): five spots in the village with one line each, the ola leaf
// (puskola) that shows the lines, and StoryIntro, which plays it before the START screen.
// The spots stand on bunds or open ground, clear of the village's trees and houses.
public static class StoryIntroSetup
{
    // Spot (Unity x, z), the way the player faces (yaw, degrees), the line, seconds, elephant.
    static readonly (Vector2 at, float yaw, string text, float seconds, bool elephant)[] Scenes =
    {
        (new Vector2(-5f, 28f), 343f, "A village in the dry zone of Sri Lanka.\nIt is harvest time.", 8f, false),
        (new Vector2(-9.25f, -12f), 25f, "The rice is ripe.\nA family's whole year grows in this field.", 8f, false),
        (new Vector2(-9.25f, -35f), 180f, "At night, wild elephants come out of the jungle.\nThey are hungry, not cruel.", 11f, true),
        (new Vector2(9.25f, -8.75f), 313f, "Tonight you keep watch from the pela,\nthe farmer's watch hut.", 8f, false),
        (new Vector2(0f, -7f), 0f, "Keep everyone alive until dawn:\nyou, your neighbour and the elephants.", 8f, false),
    };

    // The walking elephant in the jungle scene: along the jungle edge, just south of the field.
    static readonly Vector3 ElephantFrom = new Vector3(-26f, 0f, -54f);
    static readonly Vector3 ElephantTo = new Vector3(8f, 0f, -54f);

    // Old palm leaf, brown ink, a red cord.
    static readonly Color LeafColor = new Color(0.86f, 0.74f, 0.48f);
    static readonly Color InkColor = new Color(0.22f, 0.13f, 0.06f);

    public static void Build(Transform game)
    {
        var root = EditorKit.Group("StoryIntro", game);
        var intro = root.gameObject.AddComponent<StoryIntro>();

        var spots = EditorKit.Group("Spots", root);
        intro.scenes = new StoryIntro.Scene[Scenes.Length];
        for (int i = 0; i < Scenes.Length; i++)
        {
            var s = Scenes[i];
            var spot = EditorKit.Empty($"Spot_{i + 1}", spots, FarmLayout.OnGround(new Vector3(s.at.x, 0f, s.at.y)));
            spot.rotation = Quaternion.Euler(0f, s.yaw, 0f);
            intro.scenes[i] = new StoryIntro.Scene { spot = spot, text = s.text, seconds = s.seconds, elephant = s.elephant };
        }

        intro.leaf = BuildLeaf(root, intro, out var text);
        intro.leafText = text;
        // Transform.Find also finds the hidden elephant template (GameObject.Find does not).
        intro.startSpot = game.Find("GroundPrep/StartSpot");
        intro.elephantModel = game.Find("ElephantTemplate/Model")?.gameObject;
        intro.elephantFrom = ElephantFrom;
        intro.elephantTo = ElephantTo;
        if (intro.startSpot == null || intro.elephantModel == null)
            Debug.LogWarning("Night Watch: StoryIntro is missing the start spot or the elephant model. Build the whole game.");
    }

    // An ola leaf: a long pale strip with two ruled lines, two string holes and a red cord, the
    // line of text in brown ink between the holes, and a small "Skip story" button below.
    // Built at the origin facing +Z (read by a player looking along +Z); StoryIntro moves it.
    static Transform BuildLeaf(Transform root, StoryIntro intro, out TextMeshPro text)
    {
        var leaf = EditorKit.Group("OlaLeaf", root);
        var rotation = Quaternion.identity;
        var ink = EditorKit.Mat("OlaInk", InkColor, true);

        EditorKit.Board("Leaf", leaf, Vector3.zero, rotation, new Vector2(2.4f, 0.52f), LeafColor);
        foreach (var y in new[] { -0.21f, 0.21f })
            EditorKit.Part("Rule", PrimitiveType.Cube, leaf, new Vector3(0f, y, 0.01f), new Vector3(2.25f, 0.008f, 0.002f), ink, false);
        foreach (var x in new[] { -1.02f, 1.02f })
        {
            var hole = EditorKit.Part("StringHole", PrimitiveType.Cylinder, leaf, new Vector3(x, 0f, 0.01f), new Vector3(0.045f, 0.002f, 0.045f), ink, false);
            hole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        EditorKit.Part("Cord", PrimitiveType.Cylinder, leaf, new Vector3(-1.02f, -0.22f, 0f), new Vector3(0.012f, 0.22f, 0.012f),
            EditorKit.Mat("OlaCord", new Color(0.55f, 0.08f, 0.06f), true), false);

        text = EditorKit.Text("Text", leaf, new Vector3(0f, 0f, 0f), rotation, new Vector2(1.85f, 0.36f), "", 1.1f,
            TextAlignmentOptions.Center);
        text.color = InkColor;
        text.fontStyle = FontStyles.Italic;

        // "Skip story", below the leaf.
        var buttonPos = new Vector3(0.95f, -0.38f, -0.02f);
        var button = EditorKit.Part("SkipButton", PrimitiveType.Cube, leaf, buttonPos, new Vector3(0.42f, 0.1f, 0.04f),
            EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)));
        var box = button.GetComponent<BoxCollider>();
        box.size = new Vector3(1.2f, 2f, 2f);
        button.AddComponent<XRSimpleInteractable>();
        var skip = button.AddComponent<SkipStoryButton>();
        skip.story = intro;
        skip.highlightColor = new Color(1f, 1f, 0.2f);
        EditorKit.Text("SkipLabel", leaf, buttonPos + new Vector3(0f, 0f, -0.03f), rotation, new Vector2(0.38f, 0.08f),
            "Skip story", 0.8f, TextAlignmentOptions.Center);

        leaf.gameObject.SetActive(false);
        return leaf;
    }
}
