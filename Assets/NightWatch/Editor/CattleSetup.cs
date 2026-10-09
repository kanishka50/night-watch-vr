using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The village cattle at the kamatha: a brown cow and a black bull from Quaternius "Ultimate
// Animated Animal Pack" (CC0, glb from Poly Pizza, about 2,400 triangles each), each tied by a
// rope to its own post. CattleLife moves them: grazing, standing, sniffing, slow walks round the
// post, mooing, and stepping away from elephants.
// The clips come straight from the glb (glTFast imports them for the Animator, looping). The old
// cow (Farm Animals pack) only had a 6 s breathing Idle (head turned 4 degrees), so from the hut
// it looked frozen; these clips graze with the neck down 54 degrees and swish the tail.
// No free low-poly zebu (humped Asian cattle) with clips was found, so these are humpless.
public static class CattleSetup
{
    const string Folder = "Assets/NightWatch/Characters/Cattle";
    const string ControllerPath = Folder + "/CattleAnimator.controller";
    // Which file each animal uses and how tall it stands (head and horns, metres): Sri Lankan
    // village cattle are small (about 1.1 to 1.25 m at the shoulder).
    static readonly (string file, float height)[] Animals = { ("VillageCow", 1.3f), ("VillageBull", 1.4f) };
    // Animator states: name > clip in the file.
    static readonly (string state, string clip)[] States =
    {
        ("Eating", "Eating"), ("Idle", "Idle"), ("Idle_2", "Idle_2"), ("Idle_Headlow", "Idle_Headlow"),
        ("Walk", "Walk"), ("Flinch", "Idle_HitReact_Left"),
    };
    const float Rope = 2.5f;
    static int placed;

    public static void Begin() => placed = 0;

    // Places the next animal standing at `at`, facing `facing`, tied to the post at `post`.
    // keepOut: places it must not walk into (x, z, radius).
    public static void Place(Transform parent, Vector3 at, Quaternion facing, Vector3 post, float postHeight, Vector3[] keepOut)
    {
        var (file, height) = Animals[placed % Animals.Length];
        string path = $"{Folder}/{file}.glb";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            Debug.LogWarning($"Night Watch: no {path}; the kamatha has one animal less.");
            placed++;
            return;
        }
        var root = new GameObject(file.Replace("Village", "")).transform;
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

        var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin != null && skin.sharedMesh != null)
        {
            // Real size, hooves on the ground (measured on the mesh in its rest pose).
            var box = WorldBox(skin);
            if (box.size.y > 0.0001f) body.transform.localScale = Vector3.one * (height / box.size.y);
            box = WorldBox(skin);
            body.transform.position += Vector3.up * (at.y - box.min.y);
            box = WorldBox(skin);

            // Matt hide (the file's colours, rough): no plastic shine under the torch.
            var mats = skin.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null) mats[i] = VillageBuilder.RoughCopy(mats[i], $"{file}_{mats[i].name}", mats[i].name.StartsWith("Eye") ? 0.3f : 0.85f);
            skin.sharedMaterials = mats;
            skin.updateWhenOffscreen = false;

            // Something to bump into (it moves with the animal).
            var hit = root.gameObject.AddComponent<BoxCollider>();
            hit.center = root.InverseTransformPoint(box.center);
            hit.size = new Vector3(0.7f, box.size.y * 0.9f, Mathf.Max(box.size.x, box.size.z) * 0.9f);
            var rb = root.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            // Point to learn: a little bigger than the body.
            LearnSetup.Spot(root, "cattle", box.center, root.rotation, new Vector3(1.1f, box.size.y * 1.1f, Mathf.Max(box.size.x, box.size.z) * 1.15f));
        }

        var animator = body.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = Controller(path);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // Its voice: a 3D sound heard across the field, quiet far away.
        var voice = root.gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Logarithmic;
        voice.minDistance = 3f;
        voice.maxDistance = 50f;
        voice.volume = 0.7f;

        // The rope from the top of the post to the neck.
        var ropeGo = new GameObject("Rope");
        ropeGo.transform.SetParent(root, false);
        var line = ropeGo.AddComponent<LineRenderer>();
        line.positionCount = 7;
        line.useWorldSpace = true;
        line.widthMultiplier = 0.025f;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.sharedMaterial = EditorKit.Mat("CattleRope", new Color(0.55f, 0.45f, 0.30f));
        var top = post + Vector3.up * (postHeight - 0.08f);
        var neck = Bone(body.transform, "Neck2") ?? head;
        for (int i = 0; i < line.positionCount; i++)
            line.SetPosition(i, Vector3.Lerp(top, neck != null ? neck.position : at, i / (float)(line.positionCount - 1)));

        var life = root.gameObject.AddComponent<CattleLife>();
        life.post = new Vector3(post.x, at.y, post.z);
        life.reach = Rope - 0.5f;
        life.keepOut = keepOut;
        life.animator = animator;
        life.voice = voice;
        life.neck = neck;
        life.rope = line;
        life.ropeTop = top;
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

    // One controller per model file (the clips belong to their file), made fresh each build.
    static RuntimeAnimatorController Controller(string modelPath)
    {
        string path = ControllerPath.Replace("CattleAnimator", System.IO.Path.GetFileNameWithoutExtension(modelPath) + "Animator");
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && !clip.name.Contains("|"))
                clips[clip.name] = clip;
        foreach (var (state, clipName) in States)
        {
            if (!clips.TryGetValue(clipName, out var clip))
            {
                Debug.LogWarning($"Night Watch: {modelPath} has no {clipName} clip.");
                continue;
            }
            var s = machine.AddState(state);
            s.motion = clip;
            if (state == "Eating") machine.defaultState = s;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }
}
