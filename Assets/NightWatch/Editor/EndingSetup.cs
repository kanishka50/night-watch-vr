using TMPro;
using UnityEditor;
using UnityEngine;

// Builds the ending card on the VR camera (called by Night Watch > Build Full Game): a title and
// one short line, 0.8 m in front of the eyes, shown on the black screen at the end of the night.
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
        var title = Text("Title", card.transform, new Vector3(0f, 0.07f, 0.8f), new Vector2(0.9f, 0.14f), 1.2f);
        var line = Text("Line", card.transform, new Vector3(0f, -0.06f, 0.8f), new Vector2(0.8f, 0.12f), 0.45f);

        var ending = root.AddComponent<EndingCard>();
        ending.card = card;
        ending.title = title;
        ending.line = line;
        ending.sound = sound;
    }

    static TextMeshPro Text(string name, Transform parent, Vector3 localPosition, Vector2 size, float maxSize)
    {
        var text = EditorKit.Text(name, parent, Vector3.zero, Quaternion.identity, size, "", maxSize, TextAlignmentOptions.Center);
        text.transform.localPosition = localPosition;
        text.transform.localRotation = Quaternion.identity;
        return text;
    }
}
