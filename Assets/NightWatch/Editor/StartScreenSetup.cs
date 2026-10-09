using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the start screen (called by Night Watch > Build Full Game): a village notice board (an
// ola-leaf page in a wooden frame on two posts, BoardStyle) 2 m in front of the start spot, facing
// the player, with the title, one story line, the controls and a START button.
public static class StartScreenSetup
{
    public static void Build(Transform game)
    {
        var root = EditorKit.Group("StartScreen", game);
        var panel = EditorKit.Group("Panel", root);
        var start = GroundPrepSetup.StartPosition;
        var ground = FarmLayout.OnGround(start + new Vector3(0f, 0f, 2f)).y;
        var center = new Vector3(start.x, ground + 1.65f, start.z + 2f);
        var rotation = Quaternion.identity;   // read by a player looking north
        var size = new Vector2(1.9f, 1.2f);

        BoardStyle.Board("Background", panel, center, rotation, size);
        // Two posts into the ground, like a village notice board.
        var postWood = EditorKit.Mat("BoardFrame", new Color(0.3f, 0.2f, 0.12f));
        float postTop = center.y - size.y / 2f;
        foreach (var x in new[] { -size.x / 2f + 0.12f, size.x / 2f - 0.12f })
            EditorKit.Part("Post", PrimitiveType.Cube, panel, new Vector3(center.x + x, (ground + postTop) / 2f, center.z + 0.06f),
                new Vector3(0.09f, postTop - ground + 0.1f, 0.09f), postWood);   // solid

        BoardStyle.Title(EditorKit.Text("Title", panel, center + new Vector3(0f, 0.42f, 0f), rotation, new Vector2(1.6f, 0.2f),
            "NIGHT WATCH", 2.2f, TextAlignmentOptions.Center));
        BoardStyle.BodyText(EditorKit.Text("Story", panel, center + new Vector3(0f, 0.19f, 0f), rotation, new Vector2(1.7f, 0.22f),
            "You are a farmer in Sri Lanka. Tonight wild elephants will come for your paddy.\n" +
            "Keep everyone alive until dawn: you, your neighbour and the elephants.", 0.9f, TextAlignmentOptions.Center));
        // One control per line pair, so nothing wraps in the middle of a key name.
        BoardStyle.BodyText(EditorKit.Text("Controls", panel, center + new Vector3(0f, -0.12f, 0f), rotation, new Vector2(1.7f, 0.34f),
            "<b>Look</b>: turn your head / hold right mouse        <b>Walk</b>: walk / W A S D\n" +
            "<b>Teleport</b>: thumbstick forward / I        <b>Point</b> at a thing (it turns yellow)\n" +
            "<b>Grab</b> (grip / G) to take or press it        <b>Use</b> (trigger / T) what you hold\n" +
            "<b>Left hand</b>: Shift + G / T", 0.7f, TextAlignmentOptions.Center));

        // The START button below the text.
        var buttonPos = center + new Vector3(0f, -0.43f, -0.06f);
        var button = EditorKit.Part("StartButton", PrimitiveType.Cube, panel, buttonPos, new Vector3(0.56f, 0.16f, 0.08f),
            EditorKit.ButtonMat("ButtonStart", EditorKit.ButtonGreen));
        // A bigger hit box than the button, so the pointer finds it easily.
        var box = button.GetComponent<BoxCollider>();
        box.size = new Vector3(1.2f, 2f, 2f);
        button.AddComponent<XRSimpleInteractable>();
        var press = button.AddComponent<StartButton>();
        press.highlightColor = new Color(1f, 1f, 0.2f);
        EditorKit.ButtonLabel(EditorKit.Text("Label", panel, buttonPos + new Vector3(0f, 0f, -0.05f), rotation, new Vector2(0.5f, 0.13f),
            "START", 1.3f, TextAlignmentOptions.Center));

        var screen = root.gameObject.AddComponent<StartScreen>();
        screen.panel = panel.gameObject;
    }
}
