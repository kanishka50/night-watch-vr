using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

// Comfort settings for the player (course week 5: teleport, snap turn and hiding the jump):
//  - ComfortFade on the rig: the view fades to black and back around every teleport, and dims
//    for a snap turn (the black panel of ScreenFeedback, in front of the camera)
//  - a short delay before the teleport and the turn, so the view is dark when it jumps
//  - our own moves (ladder, start, story, debrief) fade the same way (PlayAreaLimit.MoveHeadTo)
// Used instead of the XR Interaction Toolkit's tunneling vignette. Called by Night Watch >
// Build Full Game.
public static class ComfortSetup
{
    public static void Build()
    {
        var rig = GameObject.Find("XR Origin (XR Rig)");
        var cam = rig != null ? rig.GetComponentInChildren<Camera>(true) : null;
        if (cam == null)
        {
            Debug.LogWarning("Night Watch: no rig camera, comfort fade not added.");
            return;
        }

        // The old tunnel vignette, from earlier builds.
        var old = cam.transform.Find("TunnelingVignette");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var teleport = rig.GetComponentInChildren<TeleportationProvider>(true);
        var snapTurn = rig.GetComponentInChildren<SnapTurnProvider>(true);

        var fade = rig.GetComponent<ComfortFade>();
        if (fade == null) fade = Undo.AddComponent<ComfortFade>(rig);
        Undo.RecordObject(fade, "Comfort fade");
        fade.teleport = teleport;
        fade.snapTurn = snapTurn;
        PrefabUtility.RecordPrefabInstancePropertyModifications(fade);

        // Wait for the view to darken before it jumps.
        if (teleport != null)
        {
            Undo.RecordObject(teleport, "Teleport delay");
            teleport.delayTime = ScreenFeedback.BlinkOutSeconds;
            PrefabUtility.RecordPrefabInstancePropertyModifications(teleport);
        }
        if (snapTurn != null)
        {
            Undo.RecordObject(snapTurn, "Snap turn delay");
            snapTurn.delayTime = 0.08f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(snapTurn);
        }
    }
}
