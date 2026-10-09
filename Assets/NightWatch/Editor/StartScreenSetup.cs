using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the start screen (called by Night Watch > Build Full Game): a dark board 2 m in front of
// the start spot, facing the player, with the title, one story line, the controls and a START button.
public static class StartScreenSetup
{
    public static void Build(Transform game)
    {
        var root = EditorKit.Group("StartScreen", game);
        var panel = EditorKit.Group("Panel", root);
        var start = GroundPrepSetup.StartPosition;
        var center = start + new Vector3(0f, 1.6f, 2f);
        var rotation = Quaternion.identity;   // read by a player looking north

        EditorKit.Board("Background", panel, center, rotation, new Vector2(1.6f, 1.1f), new Color(0.07f, 0.08f, 0.10f));
        // A wooden back, so it looks like a sign board from behind.
        EditorKit.Part("Back", PrimitiveType.Cube, panel, center + Vector3.forward * 0.04f, new Vector3(1.66f, 1.16f, 0.03f),
            EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)), false);

        var title = EditorKit.Text("Title", panel, center + new Vector3(0f, 0.4f, 0f), rotation, new Vector2(1.4f, 0.18f),
            "NIGHT WATCH", 2f, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.75f, 0.35f);
        EditorKit.Text("Story", panel, center + new Vector3(0f, 0.17f, 0f), rotation, new Vector2(1.4f, 0.22f),
            "You are a farmer in Sri Lanka. Tonight wild elephants will come for your paddy.\n" +
            "Keep everyone alive until dawn: you, your neighbour and the elephants.", 0.9f, TextAlignmentOptions.Center);
        EditorKit.Text("Controls", panel, center + new Vector3(0f, -0.13f, 0f), rotation, new Vector2(1.3f, 0.3f),
            "<b>Look</b>: turn your head / hold right mouse      <b>Walk</b>: walk / W A S D      <b>Teleport</b>: thumbstick forward / I\n" +
            "<b>Point</b> at a thing (it turns yellow), then <b>Grab</b> (grip / G) to take or press it\n" +
            "<b>Use</b> (trigger / T) what you hold      <b>Left hand</b>: Shift + G / T", 0.75f, TextAlignmentOptions.Center);

        // The START button below the text.
        var buttonPos = center + new Vector3(0f, -0.42f, -0.06f);
        var button = EditorKit.Part("StartButton", PrimitiveType.Cube, panel, buttonPos, new Vector3(0.5f, 0.14f, 0.08f),
            EditorKit.ButtonMat("ButtonStart", EditorKit.ButtonGreen));
        // A bigger hit box than the button, so the pointer finds it easily.
        var box = button.GetComponent<BoxCollider>();
        box.size = new Vector3(1.2f, 2f, 2f);
        button.AddComponent<XRSimpleInteractable>();
        var press = button.AddComponent<StartButton>();
        press.highlightColor = new Color(1f, 1f, 0.2f);
        EditorKit.ButtonLabel(EditorKit.Text("Label", panel, buttonPos + new Vector3(0f, 0f, -0.05f), rotation, new Vector2(0.45f, 0.12f),
            "START", 1.2f, TextAlignmentOptions.Center));

        var screen = root.gameObject.AddComponent<StartScreen>();
        screen.panel = panel.gameObject;
    }
}
