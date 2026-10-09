using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the neighbour's side of the story (called by Night Watch > Build Full Game, after the
// NavMesh is baked), on the back of the village house hut_c (FarmLayout.NeighbourHouse), the
// side that faces our hut:
//  - a back door that opens and a window that glows, the bottle lamp by the door, the neighbour
//    (a Sri Lankan farmer with a lamp, animated), the evening fire in the back yard and the rice store
//  - the places they walk between (door, fire, yard, the far side of the yard) and where the
//    bull goes (the store). The bull comes in through the garden gate on the village side.
//  - the phone on the hut's west rail, to call and warn the neighbour
//  - NeighbourHouse, which runs it all
// Points are given in the house's frame: x along the back wall, z out from it (negative = yard).
public static class NeighbourSetup
{
    public static void Build(Transform game)
    {
        var root = EditorKit.Group("Neighbour", game);
        float back = FarmLayout.NeighbourBackWall;
        float floor = FarmLayout.NeighbourFloor;
        Vector3 At(float x, float z) => FarmLayout.AtNeighbour(x, z);
        // Turned to face out of the back wall (toward our hut).
        var outward = Quaternion.Euler(0f, FarmLayout.NeighbourHouseYaw + 180f, 0f);

        // The back door: a frame on the wall, the hinge inside it on the door's left edge.
        const float doorX = -1f;
        var frame = EditorKit.Empty("BackDoor", root, At(doorX, back - 0.02f) + Vector3.up * floor);
        frame.rotation = outward;
        EditorKit.Part("Doorway", PrimitiveType.Cube, frame, new Vector3(0f, 0.95f, 0.01f), new Vector3(0.9f, 1.9f, 0.04f),
            EditorKit.Mat("Doorway", new Color(0.05f, 0.04f, 0.03f)), false);
        var hinge = EditorKit.Group("DoorHinge", frame);
        hinge.localPosition = new Vector3(-0.45f, 0f, 0.05f);
        EditorKit.Part("Door", PrimitiveType.Cube, hinge, new Vector3(0.45f, 0.95f, 0f), new Vector3(0.9f, 1.9f, 0.05f),
            EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)), false);

        // The window that glows when the neighbour is safe inside (NightLighting sets its glow).
        var windowAt = EditorKit.Empty("BackWindow", root, At(1.3f, back - 0.03f) + Vector3.up * (floor + 1.4f));
        windowAt.rotation = outward;
        var window = EditorKit.Part("Window", PrimitiveType.Cube, windowAt, Vector3.zero, new Vector3(0.9f, 0.7f, 0.04f),
            EditorKit.Mat("HouseWindow", new Color(1f, 0.85f, 0.45f)), false);

        var bottleLamp = BuildBottleLamp(root, At(doorX - 0.75f, back - 0.08f) + Vector3.up * (floor + 1.55f));
        bottleLamp.transform.rotation = outward;

        // The back yard, between the house and the garden fence on the field side.
        var doorPoint = EditorKit.Empty("DoorPoint", root, At(doorX, back - 0.8f));
        var fire = BuildYardFire(root, At(-2.8f, -6f));
        var firePoint = EditorKit.Empty("FirePoint", root, At(-2.2f, -5.3f));
        var yardPoint = EditorKit.Empty("YardPoint", root, At(-0.5f, -4.6f));
        var patrolPoint = EditorKit.Empty("PatrolPoint", root, At(-3f, -7f));
        var storePoint = EditorKit.Empty("StorePoint", root, At(2.6f, back - 1.5f));

        // Rice sacks against the back wall: what the elephant comes for.
        var jute = EditorKit.Mat("RiceSack", new Color(0.62f, 0.52f, 0.34f));
        var sacks = EditorKit.Empty("RiceStore", root, At(2.6f, back - 0.3f));
        sacks.rotation = outward;
        EditorKit.Part("RiceSack", PrimitiveType.Cube, sacks, new Vector3(-0.45f, 0.25f, 0f), new Vector3(0.7f, 0.5f, 0.5f), jute, false);
        EditorKit.Part("RiceSack", PrimitiveType.Cube, sacks, new Vector3(0.45f, 0.25f, 0f), new Vector3(0.7f, 0.5f, 0.5f), jute, false);
        EditorKit.Part("RiceSack", PrimitiveType.Cube, sacks, new Vector3(0f, 0.7f, 0f), new Vector3(0.6f, 0.4f, 0.45f), jute, false);

        var figure = BuildFigure(root, doorPoint.position, out var lampPivot, out var lamp, out var animator);

        var house = root.gameObject.AddComponent<NeighbourHouse>();
        house.door = hinge;
        house.window = window.GetComponent<Renderer>();
        house.figure = figure;
        house.body = animator;
        house.lampPivot = lampPivot;
        house.lamp = lamp;
        house.bottleLamp = bottleLamp;
        house.yardFire = fire;
        house.doorPoint = doorPoint;
        house.firePoint = firePoint;
        house.yardPoint = yardPoint;
        house.patrolPoint = patrolPoint;
        house.storePoint = storePoint;

        BuildPhone();
    }

    // A kuppi lampuwa: a small glass bottle with a wick and a flame, hung on a hook by the door.
    // A soft, small light, not a big glare on the wall. Hidden until someone is safe inside.
    static GameObject BuildBottleLamp(Transform root, Vector3 at)
    {
        var lamp = EditorKit.Empty("BottleLamp", root, at);
        EditorKit.Part("Hook", PrimitiveType.Cube, lamp, new Vector3(0f, 0.12f, -0.03f), new Vector3(0.02f, 0.06f, 0.06f),
            EditorKit.Mat("Switch", new Color(0.08f, 0.08f, 0.08f)), false);
        EditorKit.Part("Bottle", PrimitiveType.Cylinder, lamp, Vector3.zero, new Vector3(0.07f, 0.07f, 0.07f),
            EditorKit.Mat("BottleGlass", new Color(0.35f, 0.5f, 0.4f)), false);
        EditorKit.Part("Flame", PrimitiveType.Sphere, lamp, new Vector3(0f, 0.1f, 0f), new Vector3(0.04f, 0.07f, 0.04f),
            EditorKit.Mat("FireCore", new Color(1f, 0.85f, 0.4f), true), false);
        var lightGo = new GameObject("LampLight");
        lightGo.transform.SetParent(lamp, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.12f, 0.15f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.7f, 0.4f);
        light.range = 4f;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;
        lamp.gameObject.SetActive(false);
        return lamp.gameObject;
    }

    // A small evening fire: a few sticks and a flame, with light and crackle. Off until lit.
    // Small at first; after a phone warning the neighbour builds it up into a big fire.
    static GameObject BuildYardFire(Transform root, Vector3 at)
    {
        var sticks = EditorKit.Empty("YardFireSticks", root, at);
        var wood = EditorKit.Mat("Firewood", new Color(0.40f, 0.27f, 0.15f));
        for (int i = 0; i < 3; i++)
        {
            var rotation = Quaternion.Euler(0f, i * 120f, 0f) * Quaternion.Euler(-25f, 0f, 0f);
            var stick = EditorKit.Part("Stick", PrimitiveType.Cylinder, sticks, rotation * Vector3.forward * 0.15f + Vector3.up * 0.25f,
                new Vector3(0.07f, 0.3f, 0.07f), wood, false);
            stick.transform.localRotation = rotation;
        }

        var fire = EditorKit.Empty("YardFire", root, at);
        FireEffect.Build(fire, new Vector3(0f, 0.1f, 0f), 0.5f);
        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(fire, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.25f);
        light.range = 6f;
        light.intensity = 2f;
        light.shadows = LightShadows.None;
        var crackle = fire.gameObject.AddComponent<AudioSource>();
        crackle.spatialBlend = 1f;
        crackle.minDistance = 1f;
        crackle.maxDistance = 25f;
        crackle.volume = 0.5f;
        crackle.loop = true;
        crackle.playOnAwake = true;
        return fire.gameObject;
    }

    // A simple standing person, feet at the pivot, facing +Z: sarong, shirt, head, arms, and a
    // lamp held out in the right hand. No colliders.
    // ---------------- The neighbour: a Sri Lankan farmer ----------------
    // Quaternius "Animated Human" (CC0, OpenGameArt), repainted with our own palette: an off-white
    // banian, a dark blue sarong colour, black hair, brown skin, a white cloth tied round the head.
    // Its clips: Idle, Walk, Run, Working (bending to the fire), Death (the bull kills the
    // neighbour). The lamp hangs from the right hand. Falls back to the old block figure
    // if the model is missing.

    const string CharacterFolder = "Assets/NightWatch/Characters/Neighbour";
    const string FarmerModel = CharacterFolder + "/AnimatedHuman.fbx";
    const string FarmerPalette = CharacterFolder + "/NeighbourPalette.png";
    const string FarmerController = CharacterFolder + "/NeighbourAnimator.controller";
    const float FarmerHeight = 1.65f;
    static readonly string[] FarmerClips = { "Idle", "Walk", "Run", "Working", "Death" };
    static readonly string[] LoopingClips = { "Idle", "Walk", "Run", "Working" };

    static Transform BuildFigure(Transform root, Vector3 at, out Transform lampPivot, out Light lamp, out Animator animator)
    {
        animator = null;
        PrepareFarmerModel();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FarmerModel);
        if (model == null)
        {
            Debug.LogWarning($"Night Watch: no {FarmerModel}; the neighbour is the old block figure.");
            return BuildBlockFigure(root, at, out lampPivot, out lamp);
        }

        var figure = EditorKit.Empty("NeighbourFigure", root, at);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(model, figure);
        body.name = "Farmer";
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        // Face forward (+z): the toes point the way the farmer faces.
        var foot = FindBone(body.transform, "LeftFoot");
        var toe = FindBone(body.transform, "LeftToe_End");
        if (foot != null && toe != null && figure.InverseTransformDirection(toe.position - foot.position).z < 0f)
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // Real size: the top of the head at 1.65 m, the soles on the ground.
        var top = FindBone(body.transform, "HeadTop_End");
        float height = top != null ? top.position.y - figure.position.y : 0f;
        if (height > 0.01f) body.transform.localScale = Vector3.one * (FarmerHeight / height);
        float lowest = float.MaxValue;
        foreach (var t in body.GetComponentsInChildren<Transform>())
            if (t.name.Contains("Toe") || t.name.Contains("Foot")) lowest = Mathf.Min(lowest, t.position.y);
        if (lowest < float.MaxValue) body.transform.position += Vector3.up * (figure.position.y - lowest);

        var skin = FarmerMaterial();
        foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = skin;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        animator = body.GetComponent<Animator>();
        if (animator == null) animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = FarmerAnimator();
        animator.applyRootMotion = false;   // NeighbourHouse moves the figure; the clips walk on the spot
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        TieHeadCloth(body.transform, figure);

        // The bottle lamp hangs from the right hand (NeighbourHouse keeps it upright).
        var hand = FindBone(body.transform, "RightHand");
        lampPivot = EditorKit.Group("LampPivot", hand != null ? hand : figure);
        if (hand == null) lampPivot.localPosition = new Vector3(0.28f, 0.98f, 0.3f);
        lampPivot.rotation = figure.rotation;
        // The hand bone carries the model's own scale; undo it, so the lamp is built in metres
        // (without this the 12 cm lamp grew into a big dark box).
        float handScale = lampPivot.parent.lossyScale.x;
        if (handScale > 0.0001f) lampPivot.localScale = Vector3.one / handScale;
        lamp = BuildHandLamp(lampPivot);
        return figure;
    }

    // A white cotton cloth (a towel) tied round the head, the knot at the back, as farmers wear
    // it in the sun and at night against the dew.
    static void TieHeadCloth(Transform body, Transform figure)
    {
        var head = FindBone(body, "Head");
        var top = FindBone(body, "HeadTop_End");
        if (head == null || top == null) return;
        float size = Vector3.Distance(head.position, top.position);
        var cloth = EditorKit.Mat("HeadCloth", new Color(0.92f, 0.90f, 0.85f));
        cloth.SetFloat("_Smoothness", 0.05f);
        var center = Vector3.Lerp(head.position, top.position, 0.55f);
        var band = EditorKit.Part("HeadCloth", PrimitiveType.Cylinder, head, Vector3.zero,
            new Vector3(size * 1.05f, size * 0.14f, size * 1.12f), cloth, false);
        band.transform.SetPositionAndRotation(center, figure.rotation);
        band.transform.localScale = new Vector3(size * 1.05f, size * 0.14f, size * 1.12f) / Mathf.Max(0.0001f, head.lossyScale.x);
        var knot = EditorKit.Part("ClothKnot", PrimitiveType.Sphere, head, Vector3.zero, Vector3.one, cloth, false);
        knot.transform.position = center - figure.forward * size * 0.55f;
        knot.transform.localScale = Vector3.one * (size * 0.3f) / Mathf.Max(0.0001f, head.lossyScale.x);
    }

    static Light BuildHandLamp(Transform lampPivot)
    {
        EditorKit.Part("Handle", PrimitiveType.Cube, lampPivot, new Vector3(0f, -0.05f, 0f), new Vector3(0.02f, 0.1f, 0.02f),
            EditorKit.Mat("Switch", new Color(0.08f, 0.08f, 0.08f)), false);
        EditorKit.Part("Lamp", PrimitiveType.Cube, lampPivot, new Vector3(0f, -0.16f, 0f), new Vector3(0.12f, 0.14f, 0.12f),
            EditorKit.Mat("LampGlow", new Color(1f, 0.8f, 0.45f), true), false);
        var lightGo = new GameObject("LampLight");
        lightGo.transform.SetParent(lampPivot, false);
        lightGo.transform.localPosition = new Vector3(0f, -0.16f, 0f);
        var lamp = lightGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.7f, 0.4f);
        lamp.range = 7f;
        lamp.intensity = 2.5f;
        lamp.shadows = LightShadows.None;
        return lamp;
    }

    static Transform FindBone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // Generic rig, no imported materials (ours is set), short clip names, the walking clips loop.
    static void PrepareFarmerModel()
    {
        var importer = AssetImporter.GetAtPath(FarmerModel) as ModelImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            changed = true;
        }
        if (changed)
        {
            importer.SaveAndReimport();
            changed = false;
        }

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        var keep = new System.Collections.Generic.List<ModelImporterClipAnimation>();
        foreach (var clip in clips)
        {
            string take = clip.takeName ?? clip.name;
            string name = take.Contains("|") ? take.Substring(take.LastIndexOf('|') + 1) : take;
            if (System.Array.IndexOf(FarmerClips, name) < 0) { changed = true; continue; }
            bool loop = System.Array.IndexOf(LoopingClips, name) >= 0;
            if (clip.name != name || clip.loopTime != loop) changed = true;
            clip.name = name;
            clip.loopTime = loop;
            keep.Add(clip);
        }
        if (changed || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = keep.ToArray();
            importer.SaveAndReimport();
        }

        var texture = AssetImporter.GetAtPath(FarmerPalette) as TextureImporter;
        if (texture != null && (texture.filterMode != FilterMode.Point || texture.mipmapEnabled))
        {
            // A 32 x 32 colour palette: sharp, no blending between the colour squares.
            texture.filterMode = FilterMode.Point;
            texture.mipmapEnabled = false;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.SaveAndReimport();
        }
    }

    static Material FarmerMaterial()
    {
        var mat = EditorKit.Mat("NeighbourFarmer", Color.white);
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FarmerPalette));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static RuntimeAnimatorController FarmerAnimator()
    {
        AssetDatabase.DeleteAsset(FarmerController);
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(FarmerController);
        var machine = controller.layers[0].stateMachine;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FarmerModel))
        {
            if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
            if (System.Array.IndexOf(FarmerClips, clip.name) < 0) continue;
            var state = machine.AddState(clip.name);
            state.motion = clip;
            if (clip.name == "Idle") machine.defaultState = state;
        }
        if (machine.states.Length < FarmerClips.Length)
            Debug.LogWarning($"Night Watch: {FarmerModel} gave {machine.states.Length} of the clips {string.Join(", ", FarmerClips)}.");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // The old stand-in: a block figure (used only if the farmer model is missing).
    static Transform BuildBlockFigure(Transform root, Vector3 at, out Transform lampPivot, out Light lamp)
    {
        var figure = EditorKit.Empty("NeighbourFigure", root, at);
        var skin = EditorKit.Mat("Skin", new Color(0.45f, 0.30f, 0.20f));
        EditorKit.Part("Sarong", PrimitiveType.Cylinder, figure, new Vector3(0f, 0.425f, 0f), new Vector3(0.36f, 0.425f, 0.36f),
            EditorKit.Mat("Sarong", new Color(0.20f, 0.25f, 0.45f)), false);
        EditorKit.Part("Shirt", PrimitiveType.Cube, figure, new Vector3(0f, 1.12f, 0f), new Vector3(0.42f, 0.55f, 0.24f),
            EditorKit.Mat("Shirt", new Color(0.85f, 0.85f, 0.80f)), false);
        EditorKit.Part("Head", PrimitiveType.Sphere, figure, new Vector3(0f, 1.55f, 0f), new Vector3(0.24f, 0.26f, 0.24f), skin, false);
        EditorKit.Part("Arm_L", PrimitiveType.Cube, figure, new Vector3(-0.26f, 1.1f, 0f), new Vector3(0.09f, 0.5f, 0.09f), skin, false);
        var armR = EditorKit.Part("Arm_R", PrimitiveType.Cube, figure, new Vector3(0.26f, 1.15f, 0.12f), new Vector3(0.09f, 0.45f, 0.09f), skin, false);
        armR.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);

        lampPivot = EditorKit.Group("LampPivot", figure);
        lampPivot.localPosition = new Vector3(0.28f, 0.98f, 0.3f);
        EditorKit.Part("Handle", PrimitiveType.Cube, lampPivot, new Vector3(0f, -0.05f, 0f), new Vector3(0.02f, 0.1f, 0.02f),
            EditorKit.Mat("Switch", new Color(0.08f, 0.08f, 0.08f)), false);
        EditorKit.Part("Lamp", PrimitiveType.Cube, lampPivot, new Vector3(0f, -0.16f, 0f), new Vector3(0.12f, 0.14f, 0.12f),
            EditorKit.Mat("LampGlow", new Color(1f, 0.8f, 0.45f), true), false);
        var lightGo = new GameObject("LampLight");
        lightGo.transform.SetParent(lampPivot, false);
        lightGo.transform.localPosition = new Vector3(0f, -0.16f, 0f);
        lamp = lightGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.7f, 0.4f);
        lamp.range = 7f;
        lamp.intensity = 2.5f;
        lamp.shadows = LightShadows.None;
        return figure;
    }

    // A mobile phone on top of the hut's west rail, near the notice board.
    static void BuildPhone()
    {
        var hutProps = GameObject.Find("HutProps");
        if (hutProps == null) return;
        float floorTop = BlockoutBuilder.HutFloorTop;
        const float standHeight = 0.75f;
        var standAt = new Vector3(-1.15f, floorTop + standHeight / 2f, -0.65f);
        EditorKit.Part("PhoneStand", PrimitiveType.Cube, hutProps.transform, standAt, new Vector3(0.3f, standHeight, 0.3f),
            EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)), false);
        var position = new Vector3(standAt.x, floorTop + standHeight + 0.005f, standAt.z);

        var phone = new GameObject("Phone");
        phone.transform.SetParent(hutProps.transform, false);
        phone.transform.position = position;
        // A bigger hit box than the phone, so the pointer finds it easily.
        var box = phone.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.05f, 0f);
        box.size = new Vector3(0.3f, 0.2f, 0.35f);

        var body = EditorKit.Part("Body", PrimitiveType.Cube, phone.transform, new Vector3(0f, 0.008f, 0f), new Vector3(0.075f, 0.015f, 0.15f),
            EditorKit.Mat("PhoneBody", new Color(0.12f, 0.12f, 0.14f)), false);
        var screen = EditorKit.Part("Screen", PrimitiveType.Cube, phone.transform, new Vector3(0f, 0.017f, 0f), new Vector3(0.065f, 0.003f, 0.12f),
            EditorKit.Mat("PhoneScreen", Color.white, true), false);

        phone.AddComponent<XRSimpleInteractable>();
        var call = phone.AddComponent<PhoneCall>();
        call.screen = screen.GetComponent<Renderer>();
        call.highlightRenderers = new[] { body.GetComponent<Renderer>() };
        call.highlightColor = new Color(1f, 1f, 0.2f);
        HutPropsSetup.PointLabel("Phone: call your neighbour", hutProps.transform, position + new Vector3(0.1f, 0.25f, 0f),
            phone.GetComponent<XRSimpleInteractable>());
    }
}
