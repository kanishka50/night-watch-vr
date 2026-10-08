using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds what the player uses and sees (called by Night Watch > Build Full Game):
//  - the tools in the hut: fire pot, fire torches, firecrackers, tin cans, flashlight,
//    and the hakka patas (a tempting wrong choice) in a basket in the corner
//  - the ladder handles (climb down, climb up)
//  - the screens: notice board, wrist status, warning panel, debrief panel with restart
//  - the managers: game flow, day and night, background sound
public static class HutPropsSetup
{
    const float FloorTop = BlockoutBuilder.HutFloorTop;
    const float TableHeight = 0.8f;
    const float TableX = 1.05f;
    static float TableTop => FloorTop + TableHeight;

    public static Light CreateSun()
    {
        var go = GameObject.Find("Sun");
        if (go == null)
        {
            go = new GameObject("Sun");
            Undo.RegisterCreatedObjectUndo(go, "Sun");
        }
        var sun = go.GetComponent<Light>();
        if (sun == null) sun = go.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.85f);
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        go.transform.rotation = Quaternion.Euler(45f, 250f, 0f);
        // The moon stays the main light while editing; the day and night script switches at run time.
        var moon = GameObject.Find("Moon");
        if (moon != null) RenderSettings.sun = moon.GetComponent<Light>();
        sun.enabled = false;
        return sun;
    }

    // ---------------- Tools ----------------

    public static void BuildTools()
    {
        var root = EditorKit.Group("HutProps", null);

        var table = EditorKit.Part("Table", PrimitiveType.Cube, root, new Vector3(TableX, FloorTop + TableHeight / 2f, 0f),
            new Vector3(0.6f, TableHeight, 2.2f), EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)));
        table.isStatic = true;

        BuildFirePot(root, new Vector3(TableX, TableTop, -0.85f));
        EditorKit.Label("Fire pot", root, new Vector3(TableX - 0.1f, TableTop + 0.55f, -0.85f));

        BuildTorch(root, new Vector3(TableX - 0.1f, TableTop + 0.04f, -0.5f));
        BuildTorch(root, new Vector3(TableX + 0.1f, TableTop + 0.04f, -0.5f));
        EditorKit.Label("Fire torches", root, new Vector3(TableX - 0.1f, TableTop + 0.3f, -0.5f));

        // Eight crackers in two rows of four.
        for (int i = 0; i < 8; i++)
            BuildCracker(root, new Vector3(TableX - 0.2f + (i % 4) * 0.13f, TableTop + 0.07f, -0.2f + (i / 4) * 0.15f));
        EditorKit.Label("Firecrackers", root, new Vector3(TableX - 0.1f, TableTop + 0.3f, -0.08f));

        BuildTins(root, new Vector3(TableX, TableTop + 0.08f, 0.35f));
        EditorKit.Label("Tin cans", root, new Vector3(TableX - 0.1f, TableTop + 0.3f, 0.35f));

        BuildFlashlight(root, new Vector3(TableX, TableTop + 0.04f, 0.75f));
        EditorKit.Label("Flashlight", root, new Vector3(TableX - 0.1f, TableTop + 0.3f, 0.75f));

        // The wrong choice sits apart, in a basket in the south-west corner.
        var corner = new Vector3(-1.05f, FloorTop, -1.05f);
        EditorKit.Part("Basket", PrimitiveType.Cylinder, root, corner + new Vector3(0f, 0.05f, 0f),
            new Vector3(0.4f, 0.05f, 0.4f), EditorKit.Mat("Basket", new Color(0.55f, 0.42f, 0.25f)));
        BuildHakka(root, corner + new Vector3(0f, 0.2f, 0f));
        EditorKit.Label("Hakka patas", root, corner + new Vector3(0.1f, 0.5f, 0.1f));

        BuildLadderHandles(root);
    }

    static GameObject Tool(string name, Transform root, Vector3 position, float mass)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = position;
        var grab = go.AddComponent<XRGrabInteractable>();   // also adds a Rigidbody
        grab.useDynamicAttach = false;
        grab.farAttachMode = InteractableFarAttachMode.Near;   // pulled into the hand
        var body = go.GetComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        return go;
    }

    static void BuildFirePot(Transform root, Vector3 position)
    {
        var pot = new GameObject("FirePot");
        pot.transform.SetParent(root, false);
        pot.transform.position = position;
        EditorKit.Part("Pot", PrimitiveType.Cylinder, pot.transform, new Vector3(0f, 0.08f, 0f),
            new Vector3(0.3f, 0.08f, 0.3f), EditorKit.Mat("Clay", new Color(0.55f, 0.30f, 0.18f)));
        FireEffect.Build(pot.transform, new Vector3(0f, 0.14f, 0f), 0.3f, false);

        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(pot.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.3f);
        light.range = 6f;
        light.intensity = 2.5f;

        // The flame zone: touch a torch or cracker here to light it.
        var zone = pot.AddComponent<SphereCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(0f, 0.3f, 0f);
        zone.radius = 0.25f;

        var crackle = pot.AddComponent<AudioSource>();
        crackle.spatialBlend = 1f;
        crackle.minDistance = 1f;
        crackle.maxDistance = 15f;
        crackle.volume = 0.5f;

        var fire = pot.AddComponent<FirePot>();
        fire.fireLight = light;
        fire.crackle = crackle;
    }

    static void BuildTorch(Transform root, Vector3 position)
    {
        var torch = Tool("FireTorch", root, position, 0.6f);
        EditorKit.Part("Stick", PrimitiveType.Cylinder, torch.transform, Vector3.zero, new Vector3(0.05f, 0.35f, 0.05f),
            EditorKit.Mat("TorchStick", new Color(0.40f, 0.28f, 0.15f)));
        EditorKit.Part("Head", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0.33f, 0f), new Vector3(0.08f, 0.05f, 0.08f),
            EditorKit.Mat("TorchHead", new Color(0.15f, 0.12f, 0.10f)));

        var flame = new GameObject("Flame");
        flame.transform.SetParent(torch.transform, false);
        flame.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        FireEffect.Build(flame.transform, new Vector3(0f, -0.1f, 0f), 0.22f, false);
        var light = flame.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.65f, 0.3f);
        light.range = 10f;
        light.intensity = 6f;
        var sound = flame.AddComponent<AudioSource>();
        sound.spatialBlend = 1f;
        sound.minDistance = 1f;
        sound.maxDistance = 15f;
        sound.volume = 0.6f;

        var ft = torch.AddComponent<FireTorch>();
        ft.flame = flame;
        ft.flameLight = light;

        // Lies flat on the table.
        torch.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    static void BuildCracker(Transform root, Vector3 position)
    {
        var cracker = Tool("Firecracker", root, position, 0.2f);
        EditorKit.Part("Body", PrimitiveType.Cylinder, cracker.transform, Vector3.zero, new Vector3(0.05f, 0.06f, 0.05f),
            EditorKit.Mat("Cracker", new Color(0.75f, 0.12f, 0.10f)));

        var fuse = new GameObject("Fuse");
        fuse.transform.SetParent(cracker.transform, false);
        fuse.transform.localPosition = new Vector3(0f, 0.09f, 0f);
        var light = fuse.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.2f);
        light.range = 1.5f;
        light.intensity = 1f;
        var sound = fuse.AddComponent<AudioSource>();
        sound.spatialBlend = 1f;
        sound.minDistance = 0.5f;
        sound.maxDistance = 10f;
        sound.volume = 0.4f;
        sound.loop = true;
        sound.playOnAwake = false;

        var fc = cracker.AddComponent<Firecracker>();
        fc.fuseLight = light;
        fc.fuseSound = sound;
    }

    static void BuildTins(Transform root, Vector3 position)
    {
        var tins = Tool("TinCans", root, position, 0.4f);
        EditorKit.Part("Can", PrimitiveType.Cylinder, tins.transform, Vector3.zero, new Vector3(0.1f, 0.07f, 0.1f),
            EditorKit.Mat("Tin", new Color(0.60f, 0.60f, 0.65f)));
        EditorKit.Part("Can2", PrimitiveType.Cylinder, tins.transform, new Vector3(0.09f, 0f, 0f), new Vector3(0.1f, 0.07f, 0.1f),
            EditorKit.Mat("Tin", new Color(0.60f, 0.60f, 0.65f)));
        tins.AddComponent<NoiseMaker>();
    }

    static void BuildFlashlight(Transform root, Vector3 position)
    {
        var torch = Tool("Flashlight", root, position, 0.3f);
        // The body lies along the forward (Z) axis, so the beam points where the hand points.
        var bodyShape = EditorKit.Part("Body", PrimitiveType.Cylinder, torch.transform, Vector3.zero, new Vector3(0.05f, 0.1f, 0.05f),
            EditorKit.Mat("FlashlightBody", new Color(0.15f, 0.15f, 0.17f)));
        bodyShape.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var beamGo = new GameObject("Beam");
        beamGo.transform.SetParent(torch.transform, false);
        beamGo.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        var beam = beamGo.AddComponent<Light>();
        beam.type = LightType.Spot;
        beam.spotAngle = 35f;
        // URP light fades with the square of distance, so a beam that reaches about 20 m needs
        // a high intensity.
        beam.range = 35f;
        beam.intensity = 120f;
        beam.color = new Color(1f, 0.97f, 0.9f);

        var fl = torch.AddComponent<Flashlight>();
        fl.beam = beam;
    }

    static void BuildHakka(Transform root, Vector3 position)
    {
        var hakka = Tool("HakkaPatas", root, position, 0.2f);
        EditorKit.Part("Ball", PrimitiveType.Sphere, hakka.transform, Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f),
            EditorKit.Mat("HakkaPatas", new Color(0.40f, 0.20f, 0.10f)));
        hakka.AddComponent<HakkaPatas>();
    }

    static void BuildLadderHandles(Transform root)
    {
        var rope = EditorKit.Mat("Rope", new Color(0.75f, 0.65f, 0.40f));
        var half = BlockoutBuilder.HutSize / 2f;

        var groundSpot = EditorKit.Empty("GroundSpot", root, new Vector3(0f, 0f, -half - 2f));
        var hutSpot = EditorKit.Empty("HutSpot", root, new Vector3(0f, FloorTop, 0.3f));

        var top = EditorKit.Part("LadderTop", PrimitiveType.Cube, root, new Vector3(0f, FloorTop + 0.9f, -half + 0.05f),
            new Vector3(0.7f, 0.08f, 0.08f), rope);
        WidenHandle(top);
        top.AddComponent<XRSimpleInteractable>();
        var down = top.AddComponent<LadderClimb>();
        down.goesDown = true;
        down.highlightColor = HandleHighlight;
        down.destination = groundSpot;
        EditorKit.Label("Climb down", root, new Vector3(0f, FloorTop + 1.1f, -half + 0.05f));
        down.label = root.Find("Label_Climb down").GetComponent<TextMeshPro>();

        var bottom = EditorKit.Part("LadderBottom", PrimitiveType.Cube, root, new Vector3(0f, 1.2f, -half - 0.35f),
            new Vector3(0.7f, 0.08f, 0.08f), rope);
        WidenHandle(bottom);
        bottom.AddComponent<XRSimpleInteractable>();
        var up = bottom.AddComponent<LadderClimb>();
        up.goesDown = false;
        up.highlightColor = HandleHighlight;
        up.destination = hutSpot;
        // Faces south, toward a player standing on the ground.
        EditorKit.Text("Label_Climb up", root, new Vector3(0f, 1.45f, -half - 0.4f), Quaternion.Euler(0f, 180f, 0f),
            new Vector2(0.6f, 0.12f), "Climb up", 0.6f, TextAlignmentOptions.Center);
    }

    // A thin bar is easy to miss: the pointer's cone still picks it, but the thin ray that colours
    // the line goes past it and hits a rung, so the line stays white. A bigger invisible hit box
    // (0.8 x 0.35 x 0.3 m) fixes that. A stronger yellow shows the highlight on the rope colour.
    static void WidenHandle(GameObject handle)
    {
        var box = handle.GetComponent<BoxCollider>();
        var scale = handle.transform.localScale;
        box.size = new Vector3(0.8f / scale.x, 0.35f / scale.y, 0.3f / scale.z);
    }

    static readonly Color HandleHighlight = new Color(1f, 1f, 0.2f);

    // ---------------- Screens ----------------

    public static void BuildScreens(Transform game)
    {
        var screens = EditorKit.Group("Screens", game);
        var dark = new Color(0.07f, 0.08f, 0.10f);

        // Notice board: a slanted reading desk inside the west rail, fully below the rail top,
        // tilted up toward the player. Read it by looking down; it never blocks the view of the field.
        var boardRot = Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(45f, 0f, 0f);
        var boardCenter = new Vector3(-1.2f, FloorTop + 0.6f, 0.3f);
        var board = new GameObject("NoticeBoard");
        board.transform.SetParent(screens, false);
        EditorKit.Board("Background", board.transform, boardCenter, boardRot, new Vector2(0.9f, 0.62f), dark);
        // A wooden stand under the desk, so it does not float.
        EditorKit.Part("Stand", PrimitiveType.Cube, board.transform, new Vector3(-1.25f, FloorTop + 0.2f, 0.3f),
            new Vector3(0.1f, 0.4f, 0.5f), EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)));
        var up = boardRot * Vector3.up;
        var body = EditorKit.Text("Body", board.transform, boardCenter + up * 0.06f, boardRot, new Vector2(0.84f, 0.46f), "", 0.5f);
        var alert = EditorKit.Text("Alert", board.transform, boardCenter - up * 0.21f, boardRot, new Vector2(0.84f, 0.07f), "", 0.4f);
        alert.color = new Color(1f, 0.85f, 0.3f);
        var status = EditorKit.Text("Status", board.transform, boardCenter - up * 0.28f, boardRot, new Vector2(0.84f, 0.05f), "", 0.3f);
        var nb = board.AddComponent<NoticeBoard>();
        nb.body = body;
        nb.alert = alert;
        var boardStatus = status.gameObject.AddComponent<StatusDisplay>();
        boardStatus.text = status;

        // No wrist display: in the simulator the left controller sits in front of the face and the
        // text covered the view. The status line at the bottom of the notice board is enough.
        // Remove one left by an earlier build.
        var leftHand = GameObject.Find("Left Controller");
        var oldWrist = leftHand != null ? leftHand.transform.Find("WristStatus") : null;
        if (oldWrist != null) Undo.DestroyObjectImmediate(oldWrist.gameObject);

        // Warning panel (moved in front of the player when shown).
        var warningRoot = new GameObject("WarningPanel");
        warningRoot.transform.SetParent(screens, false);
        var warningPanel = new GameObject("Panel");
        warningPanel.transform.SetParent(warningRoot.transform, false);
        EditorKit.Board("Background", warningPanel.transform, Vector3.zero, Quaternion.identity, new Vector2(1f, 0.6f), new Color(0.35f, 0.05f, 0.05f));
        var warningText = EditorKit.Text("Text", warningPanel.transform, Vector3.zero, Quaternion.identity, new Vector2(0.9f, 0.5f), "", 0.6f,
            TextAlignmentOptions.Center);
        var wp = warningRoot.AddComponent<WarningPanel>();
        wp.panel = warningPanel;
        wp.text = warningText;

        // Debrief panel north of the hut, facing the player.
        var debriefRoot = new GameObject("Debrief");
        debriefRoot.transform.SetParent(screens, false);
        var debriefPanel = new GameObject("Panel");
        debriefPanel.transform.SetParent(debriefRoot.transform, false);
        var center = new Vector3(0f, 4.4f, 7f);
        EditorKit.Board("Background", debriefPanel.transform, center, Quaternion.identity, new Vector2(6.2f, 3.6f), dark);
        var results = EditorKit.Text("Results", debriefPanel.transform, center + new Vector3(-0.85f, 0f, 0f), Quaternion.identity,
            new Vector2(4.2f, 3.3f), "", 1.4f);
        var credits = EditorKit.Text("Credits", debriefPanel.transform, center + new Vector3(2.15f, 0f, 0f), Quaternion.identity,
            new Vector2(1.7f, 3.3f), "", 1f);

        // Restart: a green block under the results panel. Reachable with the pointer from the
        // hut or from the ground (the hut may have collapsed).
        var restartPosition = new Vector3(0f, 2.3f, 6.9f);
        var restart = EditorKit.Empty("Restart", debriefRoot.transform, restartPosition).gameObject;
        EditorKit.Part("Button", PrimitiveType.Cube, restart.transform, Vector3.zero, new Vector3(0.9f, 0.3f, 0.1f),
            EditorKit.Mat("Button", new Color(0.2f, 0.6f, 0.25f)));
        restart.AddComponent<XRSimpleInteractable>();
        restart.AddComponent<RestartButton>();
        EditorKit.Text("Label", restart.transform, restartPosition + new Vector3(0f, 0f, -0.07f), Quaternion.identity,
            new Vector2(0.8f, 0.25f), "RESTART", 1.5f, TextAlignmentOptions.Center);

        var dp = debriefRoot.AddComponent<DebriefPanel>();
        dp.panel = debriefPanel;
        dp.results = results;
        dp.credits = credits;
        dp.restartButton = restart;
    }

    // ---------------- Screen effects ----------------

    // A see-through panel just in front of the VR camera, for red flashes and fades to black.
    static void BuildScreenOverlay()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("Night Watch: no main camera found, screen effects not added.");
            return;
        }
        var old = cam.transform.Find("ScreenOverlay");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overlay.name = "ScreenOverlay";
        Object.DestroyImmediate(overlay.GetComponent<Collider>());
        overlay.transform.SetParent(cam.transform, false);
        overlay.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        overlay.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        var renderer = overlay.GetComponent<Renderer>();
        renderer.sharedMaterial = OverlayMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;

        // The red wound edge: same material, just behind the fade panel (sized to the view at run time).
        var oldWound = cam.transform.Find("WoundOverlay");
        if (oldWound != null) Undo.DestroyObjectImmediate(oldWound.gameObject);
        var wound = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wound.name = "WoundOverlay";
        Object.DestroyImmediate(wound.GetComponent<Collider>());
        wound.transform.SetParent(cam.transform, false);
        wound.transform.localPosition = new Vector3(0f, 0f, 0.125f);
        var woundRenderer = wound.GetComponent<Renderer>();
        woundRenderer.sharedMaterial = OverlayMaterial();
        woundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        woundRenderer.receiveShadows = false;
        woundRenderer.enabled = false;

        var feedback = overlay.AddComponent<ScreenFeedback>();
        feedback.overlay = renderer;
        feedback.woundOverlay = woundRenderer;
    }

    // A small brown dust cloud (hidden), copied wherever an elephant hits the hut.
    static ParticleSystem BuildDustTemplate(Transform game)
    {
        var go = new GameObject("DustTemplate");
        go.transform.SetParent(game, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        main.startColor = new Color(0.45f, 0.36f, 0.25f, 0.6f);
        main.gravityModifier = -0.05f;
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        var fadeOut = new Gradient();
        fadeOut.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        colour.color = fadeOut;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));

        // URP's own particle material, so the dust is not pink.
        var mat = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;

        go.SetActive(false);
        return ps;
    }

    // A transparent, unlit material (URP) for the overlay.
    static Material OverlayMaterial()
    {
        string path = EditorKit.MaterialFolder + "/ScreenOverlay.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1f);   // transparent
        mat.SetFloat("_Blend", 0f);     // alpha blend
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
        mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------------- Managers ----------------

    public static void BuildManagers(Transform game, Light sun)
    {
        var managers = new GameObject("GameManager");
        managers.transform.SetParent(game, false);

        var gm = managers.AddComponent<GameManager>();
        var hut = GameObject.Find("WatchHut");
        if (hut != null) gm.hut = hut.transform;
        // Where the player stands to read the results if the hut is gone: south of it, facing north.
        gm.debriefSpot = EditorKit.Empty("DebriefSpot", game, new Vector3(0f, 0f, -4f));

        // When the hut collapses, the things inside it disappear with it.
        var damage = hut != null ? hut.GetComponent<HutDamage>() : null;
        if (damage != null)
        {
            var board = GameObject.Find("NoticeBoard");
            damage.hideOnCollapse = new[] { GameObject.Find("HutProps"), board };
            damage.dustTemplate = BuildDustTemplate(game);
        }

        // No background drum or heartbeat: near elephants, the player hears the elephants themselves.
        // (DangerSound and TensionDrum are kept in Scripts but not added to the scene.)

        BuildScreenOverlay();

        var cycle = managers.AddComponent<DayNightCycle>();
        cycle.sun = sun;
        var moon = GameObject.Find("Moon");
        if (moon != null) cycle.moon = moon.GetComponent<Light>();

        managers.AddComponent<AudioSource>();
        managers.AddComponent<AmbientSound>();
    }
}
