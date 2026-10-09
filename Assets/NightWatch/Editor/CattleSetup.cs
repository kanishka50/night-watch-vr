using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The village cattle at the kamatha: Quaternius "Farm Animals" cow (CC0, OpenGameArt), repainted
// from black-and-white to the fawn and cream of Sri Lankan village cattle, 1.35 m tall, standing
// tied and resting (its Idle clip, each one out of step with the other).
public static class CattleSetup
{
    const string Folder = "Assets/NightWatch/Characters/Cattle";
    const string ModelPath = Folder + "/Cow.fbx";
    const string ControllerPath = Folder + "/CattleAnimator.controller";
    const float Height = 1.35f;

    // Body colour per animal (the model's "White"), then its dark parts ("Black") and nose ("Pink").
    static readonly Color[] Coats = { new Color(0.66f, 0.50f, 0.34f), new Color(0.84f, 0.77f, 0.66f) };
    static readonly Color Dark = new Color(0.18f, 0.12f, 0.09f), Nose = new Color(0.50f, 0.38f, 0.33f);
    static int placed;

    public static GameObject Model()
    {
        Prepare();
        placed = 0;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) Debug.LogWarning($"Night Watch: no {ModelPath}; the kamatha has no cattle.");
        return model;
    }

    public static void Place(GameObject model, Transform parent, Vector3 at, Quaternion facing)
    {
        if (model == null) return;
        var root = new GameObject("Cow").transform;
        root.SetParent(parent, true);
        root.SetPositionAndRotation(at, facing);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        // Head forward (+z): from the tail to the head.
        var head = Bone(body.transform, "Head");
        var tail = Bone(body.transform, "Tail1");
        if (head != null && tail != null && root.InverseTransformDirection(head.position - tail.position).z < 0f)
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // Real size, hooves on the ground (measured on the mesh in its rest pose).
        var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin != null && skin.sharedMesh != null)
        {
            var box = WorldBox(skin);
            if (box.size.y > 0.01f) body.transform.localScale = Vector3.one * (Height / box.size.y);
            box = WorldBox(skin);
            body.transform.position += Vector3.up * (at.y - box.min.y);

            var coat = Coats[placed % Coats.Length];
            var mats = skin.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string source = mats[i] != null ? mats[i].name : "";
                var colour = source.Contains("Black") ? Dark : source.Contains("Pink") ? Nose : coat;
                string name = source.Contains("Black") ? "CattleDark" : source.Contains("Pink") ? "CattleNose" : $"CattleCoat{placed % Coats.Length}";
                var mat = EditorKit.Mat(name, colour);
                mat.SetColor("_BaseColor", colour);
                mat.SetFloat("_Smoothness", 0.12f);
                EditorUtility.SetDirty(mat);
                mats[i] = mat;
            }
            skin.sharedMaterials = mats;

            // Something to bump into.
            box = WorldBox(skin);
            var hit = root.gameObject.AddComponent<BoxCollider>();
            hit.center = root.InverseTransformPoint(box.center);
            hit.size = new Vector3(0.7f, box.size.y * 0.9f, 1.9f);
        }

        var animator = body.GetComponent<Animator>();
        if (animator == null) animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = Controller();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullCompletely;
        body.AddComponent<IdleAnimal>();
        placed++;
    }

    static Bounds WorldBox(SkinnedMeshRenderer skin)
    {
        var local = skin.sharedMesh.bounds;
        var box = new Bounds(skin.transform.TransformPoint(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = local.center + Vector3.Scale(local.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one);
            box.Encapsulate(skin.transform.TransformPoint(corner));
        }
        return box;
    }

    static Transform Bone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // Generic rig, short clip names; Idle loops.
    static void Prepare()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.SaveAndReimport();
        }
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool changed = importer.clipAnimations.Length == 0;
        foreach (var clip in clips)
        {
            string take = clip.takeName ?? clip.name;
            string name = take.Contains("|") ? take.Substring(take.LastIndexOf('|') + 1) : take;
            bool loop = name == "Idle";
            if (clip.name != name || clip.loopTime != loop) changed = true;
            clip.name = name;
            clip.loopTime = loop;
        }
        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    static RuntimeAnimatorController Controller()
    {
        var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (existing != null && placed > 0) return existing;
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (!(asset is AnimationClip clip) || clip.name != "Idle") continue;
            machine.defaultState = machine.AddState("Idle");
            machine.defaultState.motion = clip;
        }
        if (machine.defaultState == null) Debug.LogWarning($"Night Watch: {ModelPath} has no Idle clip.");
        EditorUtility.SetDirty(controller);
        return controller;
    }
}
