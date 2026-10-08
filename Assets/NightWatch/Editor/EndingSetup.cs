using TMPro;
using UnityEditor;
using UnityEngine;

// Builds the ending card on the VR camera (called by Night Watch > Build Full Game): a title and
// one short line, 1 m in front of the eyes, shown on the black screen at the end of the night.
// Kept within the middle of the view (about 35 degrees wide), where text is comfortable to read in VR.
public static class EndingSetup
{
    public static void Build()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("Night Watch: no main camera found, ending card not added.");
            return;
        }
        var old = cam.transform.Find("EndingCard");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("EndingCard");
        Undo.RegisterCreatedObjectUndo(root, "Ending card");
        root.transform.SetParent(cam.transform, false);
        var sound = root.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.spatialBlend = 0f;

        // The card itself is a child, so hiding it does not stop the sound on the root.
        var card = new GameObject("Card");
        card.transform.SetParent(root.transform, false);
        var title = Text("Title", card.transform, new Vector3(0f, 0.07f, 1f), new Vector2(0.62f, 0.1f), 0.8f);
        var line = Text("Line", card.transform, new Vector3(0f, -0.06f, 1f), new Vector2(0.56f, 0.12f), 0.35f);

        var ending = root.AddComponent<EndingCard>();
        ending.card = card;
        ending.title = title;
        ending.line = line;
        ending.sound = sound;
    }

    // The tool hint: one line, low in the view, 0.9 m away, with a dark outline so it reads on
    // any background.
    public static void BuildHint()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var old = cam.transform.Find("ToolHint");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("ToolHint");
        Undo.RegisterCreatedObjectUndo(root, "Tool hint");
        root.transform.SetParent(cam.transform, false);
        var text = Text("Text", root.transform, new Vector3(0f, -0.2f, 0.9f), new Vector2(0.55f, 0.09f), 0.3f);
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        root.AddComponent<HintDisplay>().text = text;
    }

    static TextMeshPro Text(string name, Transform parent, Vector3 localPosition, Vector2 size, float maxSize)
    {
        var text = EditorKit.Text(name, parent, Vector3.zero, Quaternion.identity, size, "", maxSize, TextAlignmentOptions.Center);
        text.transform.localPosition = localPosition;
        text.transform.localRotation = Quaternion.identity;
        return text;
    }
}
