using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

// Comfort settings for the player (course week 5: teleport, snap turn and a vignette):
//  - the XR Interaction Toolkit's tunneling vignette on the camera: the edges of the view go
//    dark for a moment on every teleport and snap turn
//  - a short delay before the teleport and the turn, so the vignette is closed when the view jumps
//  - VignetteBlink, for our own moves (ladder, start, debrief)
// Called by Night Watch > Build Full Game. Running it again replaces the old vignette.
public static class ComfortSetup
{
    const string VignettePrefab = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";

    public static void Build()
    {
        var rig = GameObject.Find("XR Origin (XR Rig)");
        var cam = rig != null ? rig.GetComponentInChildren<Camera>(true) : null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VignettePrefab);
        if (cam == null || prefab == null)
        {
            Debug.LogWarning("Night Watch: no rig camera or no TunnelingVignette prefab, comfort vignette not added.");
            return;
        }

        var old = cam.transform.Find("TunnelingVignette");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cam.transform);
        go.name = "TunnelingVignette";
        Undo.RegisterCreatedObjectUndo(go, "Comfort vignette");
        go.AddComponent<VignetteBlink>();

        var teleport = rig.GetComponentInChildren<TeleportationProvider>(true);
        var snapTurn = rig.GetComponentInChildren<SnapTurnProvider>(true);

        // Teleport: nearly closes, then opens. Snap turn: a lighter dimming of the edges.
        var controller = go.GetComponent<TunnelingVignetteController>();
        controller.locomotionVignetteProviders = new List<LocomotionVignetteProvider>
        {
            Provider(teleport, 0.15f, 0.12f, 0.35f),
            Provider(snapTurn, 0.45f, 0.08f, 0.25f),
        };
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

        // Wait for the vignette to close before the view jumps.
        if (teleport != null)
        {
            Undo.RecordObject(teleport, "Teleport delay");
            teleport.delayTime = 0.12f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(teleport);
        }
        if (snapTurn != null)
        {
            Undo.RecordObject(snapTurn, "Snap turn delay");
            snapTurn.delayTime = 0.08f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(snapTurn);
        }
    }

    static LocomotionVignetteProvider Provider(LocomotionProvider locomotion, float aperture, float easeIn, float easeOut)
    {
        return new LocomotionVignetteProvider
        {
            locomotionProvider = locomotion,
            enabled = locomotion != null,
            overrideDefaultParameters = true,
            overrideParameters = new VignetteParameters
            {
                apertureSize = aperture,
                featheringEffect = 0.2f,
                easeInTime = easeIn,
                easeOutTime = easeOut,
                easeInTimeLock = true,
                easeOutDelayTime = 0.05f,
                vignetteColor = Color.black,
                vignetteColorBlend = Color.black,
            },
        };
    }
}
