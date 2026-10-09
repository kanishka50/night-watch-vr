using System.Linq;
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

    // The tools are built from several shapes in real colours, styled on what Sri Lankan
    // farmers keep in a pela. Each keeps one collider on its main shape, so it grabs and falls as
    // before; the extra shapes (bands, rims, fuses, string) have none. Their name labels show
    // only while a pointer is on them (HoverLabel).
    public static void BuildTools()
    {
        var root = EditorKit.Group("HutProps", null);

        var table = EditorKit.Part("Table", PrimitiveType.Cube, root, new Vector3(TableX, FloorTop + TableHeight / 2f, 0f),
            new Vector3(0.6f, TableHeight, 2.2f), EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)));
        // Not static: it shakes and tilts with the hut (HutDamage.followers).

        BuildFirePot(root, new Vector3(TableX, TableTop, -0.85f));
        // No pointer target: it shows while a torch or cracker is in the hand (light it here).
        PointLabel("Fire pot", root, new Vector3(TableX - 0.1f, TableTop + 0.6f, -0.85f)).showWhileFireToolHeld = true;

        var torch1 = BuildTorch(root, new Vector3(TableX - 0.1f, TableTop + 0.04f, -0.5f));
        var torch2 = BuildTorch(root, new Vector3(TableX + 0.1f, TableTop + 0.04f, -0.5f));
        PointLabel("Fire torches", root, new Vector3(TableX - 0.1f, TableTop + 0.3f, -0.5f), torch1, torch2);

        // Eight crackers in two rows of four.
        var crackers = new XRBaseInteractable[8];
        for (int i = 0; i < 8; i++)
            crackers[i] = BuildCracker(root, new Vector3(TableX - 0.2f + (i % 4) * 0.13f, TableTop + 0.07f, -0.2f + (i / 4) * 0.15f));
        PointLabel("Firecrackers", root, new Vector3(TableX - 0.1f, TableTop + 0.3f, -0.08f), crackers);

        var tins = BuildTins(root, new Vector3(TableX, TableTop + 0.08f, 0.35f));
        PointLabel("Tin cans", root, new Vector3(TableX - 0.1f, TableTop + 0.3f, 0.35f), tins);

        var flashlight = BuildFlashlight(root, new Vector3(TableX, TableTop + 0.04f, 0.75f));
        PointLabel("Flashlight", root, new Vector3(TableX - 0.1f, TableTop + 0.3f, 0.75f), flashlight);

        // The wrong choice sits apart, in a basket in the south-west corner.
        var corner = new Vector3(-1.05f, FloorTop, -1.05f);
        EditorKit.Part("Basket", PrimitiveType.Cylinder, root, corner + new Vector3(0f, 0.05f, 0f),
            new Vector3(0.4f, 0.05f, 0.4f), EditorKit.Mat("Basket", new Color(0.55f, 0.42f, 0.25f)));
        var hakka = BuildHakka(root, corner + new Vector3(0f, 0.2f, 0f));
        PointLabel("Hakka patas", root, corner + new Vector3(0.1f, 0.45f, 0.1f), hakka);

        BuildLadderHandles(root);
    }

    // Was 0.6 (always shown, too big in the face); now small, and only shown when pointed at.
    public const float LabelSize = 0.3f;

    // A small name label that shows only while one of the targets is pointed at.
    public static HoverLabel PointLabel(string text, Transform root, Vector3 position, params XRBaseInteractable[] targets)
    {
        EditorKit.Label(text, root, position);
        var label = root.Find("Label_" + text).GetComponent<TextMeshPro>();
        label.fontSizeMax = LabelSize;
        var hover = label.gameObject.AddComponent<HoverLabel>();
        hover.targets = targets;
        return hover;
    }

    // thrown = small and thrown fast (crackers, hakka patas): checked between frames as well, so a
    // throw cannot pass through a thin rail.
    static GameObject Tool(string name, Transform root, Vector3 position, float mass, bool thrown = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = position;
        var grab = go.AddComponent<XRGrabInteractable>();   // also adds a Rigidbody
        grab.useDynamicAttach = false;
        grab.farAttachMode = InteractableFarAttachMode.Near;   // pulled into the hand
        // Physics moves the held tool to the hand, so it stops at posts, rails, the table and
        // elephants instead of passing through them (Instantaneous ignored every wall).
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        var body = go.GetComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = thrown ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Continuous;
        return go;
    }

    // A tool material: colour (when it has no photo texture), shine and metal.
    static Material ToolMat(string name, Color color, float smooth, float metal = 0f)
    {
        var mat = EditorKit.Mat(name, color);
        if (mat.GetTexture("_BaseMap") == null) mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // A decoration shape (no collider).
    static GameObject Trim(string name, PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material mat,
        Vector3 turn = default)
    {
        var part = EditorKit.Part(name, type, parent, at, size, mat, false);
        part.transform.localRotation = Quaternion.Euler(turn);
        return part;
    }

    // A clay fire pot (the kind a watchman keeps embers in): a narrow foot, a round belly,
    // a neck and a lip, with glowing embers on top and the flames rising from them.
    static void BuildFirePot(Transform root, Vector3 position)
    {
        var pot = new GameObject("FirePot");
        pot.transform.SetParent(root, false);
        pot.transform.position = position;
        var clay = ToolMat("ClayPot", new Color(0.62f, 0.33f, 0.20f), 0.15f);
        var clayDark = ToolMat("ClayPotRim", new Color(0.45f, 0.24f, 0.15f), 0.1f);
        EditorKit.Part("Foot", PrimitiveType.Cylinder, pot.transform, new Vector3(0f, 0.025f, 0f), new Vector3(0.2f, 0.025f, 0.2f), clay);
        EditorKit.Part("Belly", PrimitiveType.Sphere, pot.transform, new Vector3(0f, 0.13f, 0f), new Vector3(0.34f, 0.22f, 0.34f), clay);
        Trim("Neck", PrimitiveType.Cylinder, pot.transform, new Vector3(0f, 0.235f, 0f), new Vector3(0.24f, 0.02f, 0.24f), clay);
        Trim("Lip", PrimitiveType.Cylinder, pot.transform, new Vector3(0f, 0.255f, 0f), new Vector3(0.28f, 0.015f, 0.28f), clayDark);
        var embers = ToolMat("Embers", new Color(0.35f, 0.08f, 0.02f), 0.1f);
        embers.EnableKeyword("_EMISSION");
        embers.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.08f) * 2f);
        Trim("Embers", PrimitiveType.Cylinder, pot.transform, new Vector3(0f, 0.262f, 0f), new Vector3(0.22f, 0.006f, 0.22f), embers);
        FireEffect.Build(pot.transform, new Vector3(0f, 0.27f, 0f), 0.5f, false);

        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(pot.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.3f);
        light.range = 6f;
        light.intensity = 2.5f;

        // The flame zone: touch a torch or cracker here to light it.
        var zone = pot.AddComponent<SphereCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(0f, 0.4f, 0f);
        zone.radius = 0.28f;

        var crackle = pot.AddComponent<AudioSource>();
        crackle.spatialBlend = 1f;
        crackle.minDistance = 1f;
        crackle.maxDistance = 15f;
        crackle.volume = 0.5f;

        var fire = pot.AddComponent<FirePot>();
        fire.fireLight = light;
        fire.crackle = crackle;
    }

    // A fire torch (pandama): a bamboo pole with node rings, its head a wad of cloth bound with
    // wire, blackened at the top where it burns.
    static XRBaseInteractable BuildTorch(Transform root, Vector3 position)
    {
        var torch = Tool("FireTorch", root, position, 0.6f);
        var bamboo = ToolMat("TorchPole", new Color(0.72f, 0.62f, 0.36f), 0.3f);
        var node = ToolMat("BambooNode", new Color(0.50f, 0.42f, 0.24f), 0.2f);
        EditorKit.Part("Stick", PrimitiveType.Cylinder, torch.transform, Vector3.zero, new Vector3(0.045f, 0.36f, 0.045f), bamboo);
        foreach (float y in new[] { -0.25f, -0.05f, 0.15f })
            Trim("Node", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, y, 0f), new Vector3(0.052f, 0.006f, 0.052f), node);
        EditorKit.Part("Head", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.085f, 0.07f, 0.085f),
            ToolMat("TorchCloth", new Color(0.30f, 0.24f, 0.18f), 0.05f));
        var wire = ToolMat("Wire", new Color(0.45f, 0.45f, 0.47f), 0.5f, 0.8f);
        foreach (float y in new[] { 0.31f, 0.41f })
            Trim("Wire", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, y, 0f), new Vector3(0.09f, 0.005f, 0.09f), wire);
        Trim("Char", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0.432f, 0f), new Vector3(0.075f, 0.004f, 0.075f),
            ToolMat("Char", new Color(0.05f, 0.04f, 0.04f), 0.05f));

        var flame = new GameObject("Flame");
        flame.transform.SetParent(torch.transform, false);
        flame.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        FireEffect.Build(flame.transform, new Vector3(0f, -0.07f, 0f), 0.35f, false);
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
        return torch.GetComponent<XRBaseInteractable>();
    }

    // A firecracker (ali wedi): a red paper tube with a gold band, clay plugs at both ends and
    // a short green fuse.
    static XRBaseInteractable BuildCracker(Transform root, Vector3 position)
    {
        var cracker = Tool("Firecracker", root, position, 0.2f, true);
        EditorKit.Part("Body", PrimitiveType.Cylinder, cracker.transform, Vector3.zero, new Vector3(0.045f, 0.065f, 0.045f),
            ToolMat("CrackerPaper", new Color(0.80f, 0.10f, 0.08f), 0.15f));
        Trim("Band", PrimitiveType.Cylinder, cracker.transform, Vector3.zero, new Vector3(0.047f, 0.012f, 0.047f),
            ToolMat("CrackerBand", new Color(0.85f, 0.65f, 0.15f), 0.5f, 0.4f));
        var plug = ToolMat("CrackerPlug", new Color(0.28f, 0.20f, 0.13f), 0.05f);
        Trim("PlugTop", PrimitiveType.Cylinder, cracker.transform, new Vector3(0f, 0.066f, 0f), new Vector3(0.04f, 0.004f, 0.04f), plug);
        Trim("PlugBottom", PrimitiveType.Cylinder, cracker.transform, new Vector3(0f, -0.066f, 0f), new Vector3(0.04f, 0.004f, 0.04f), plug);
        Trim("FuseCord", PrimitiveType.Cylinder, cracker.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.006f, 0.012f, 0.006f),
            ToolMat("Fuse", new Color(0.15f, 0.45f, 0.15f), 0.2f));

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
        return cracker.GetComponent<XRBaseInteractable>();
    }

    // Two old tins tied together with coir string, a rope handle on top (banged together to
    // make noise). Rims, and faded label bands.
    static XRBaseInteractable BuildTins(Transform root, Vector3 position)
    {
        var tins = Tool("TinCans", root, position, 0.4f);
        var tin = ToolMat("TinCan", new Color(0.72f, 0.72f, 0.75f), 0.55f, 0.8f);
        var label = ToolMat("TinLabel", new Color(0.62f, 0.22f, 0.16f), 0.2f);
        var coir = ToolMat("Coir", new Color(0.55f, 0.40f, 0.22f), 0.05f);
        foreach (float x in new[] { 0f, 0.09f })
        {
            EditorKit.Part(x == 0f ? "Can" : "Can2", PrimitiveType.Cylinder, tins.transform, new Vector3(x, 0f, 0f),
                new Vector3(0.1f, 0.07f, 0.1f), tin);
            Trim("Label", PrimitiveType.Cylinder, tins.transform, new Vector3(x, 0f, 0f), new Vector3(0.102f, 0.035f, 0.102f), label);
            Trim("RimTop", PrimitiveType.Cylinder, tins.transform, new Vector3(x, 0.07f, 0f), new Vector3(0.104f, 0.004f, 0.104f), tin);
            Trim("RimBottom", PrimitiveType.Cylinder, tins.transform, new Vector3(x, -0.07f, 0f), new Vector3(0.104f, 0.004f, 0.104f), tin);
            Trim("String", PrimitiveType.Cube, tins.transform, new Vector3(x, 0.105f, 0f), new Vector3(0.006f, 0.07f, 0.006f), coir);
        }
        Trim("Handle", PrimitiveType.Cube, tins.transform, new Vector3(0.045f, 0.14f, 0f), new Vector3(0.096f, 0.006f, 0.006f), coir);
        tins.AddComponent<NoiseMaker>();
        return tins.GetComponent<XRBaseInteractable>();
    }

    static XRBaseInteractable BuildFlashlight(Transform root, Vector3 position)
    {
        var torch = Tool("Flashlight", root, position, 0.3f);
        // The body lies along the forward (Z) axis, so the beam points where the hand points.
        // A dark metal tube with rubber grip rings, a wider head, a bright bezel and a glass lens.
        var along = new Vector3(90f, 0f, 0f);
        var metal = ToolMat("TorchBody", new Color(0.12f, 0.12f, 0.13f), 0.45f, 0.6f);
        var chrome = ToolMat("Chrome", new Color(0.80f, 0.80f, 0.82f), 0.85f, 1f);
        var bodyShape = EditorKit.Part("Body", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0f, -0.02f),
            new Vector3(0.042f, 0.08f, 0.042f), metal);
        bodyShape.transform.localRotation = Quaternion.Euler(along);
        var rubber = ToolMat("Rubber", new Color(0.04f, 0.04f, 0.04f), 0.15f);
        foreach (float z in new[] { -0.08f, -0.06f, -0.04f })
            Trim("Grip", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0f, z), new Vector3(0.045f, 0.004f, 0.045f), rubber, along);
        Trim("Head", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0f, 0.085f), new Vector3(0.06f, 0.025f, 0.06f), metal, along);
        Trim("Bezel", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0f, 0.111f), new Vector3(0.064f, 0.004f, 0.064f), chrome, along);
        Trim("Lens", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0f, 0.114f), new Vector3(0.05f, 0.002f, 0.05f),
            ToolMat("Lens", new Color(0.90f, 0.92f, 0.85f), 0.95f), along);
        Trim("Tail", PrimitiveType.Cylinder, torch.transform, new Vector3(0f, 0f, -0.104f), new Vector3(0.044f, 0.006f, 0.044f), chrome, along);
        Trim("Switch", PrimitiveType.Cube, torch.transform, new Vector3(0f, 0.022f, 0f), new Vector3(0.012f, 0.008f, 0.02f),
            ToolMat("FlashlightSwitch", new Color(0.80f, 0.30f, 0.10f), 0.3f));

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
        return torch.GetComponent<XRBaseInteractable>();
    }

    // Hakka patas (the illegal "jaw bomb" hidden in food): a ball wrapped in cloth, tied round
    // with twine, a knot on top.
    static XRBaseInteractable BuildHakka(Transform root, Vector3 position)
    {
        var hakka = Tool("HakkaPatas", root, position, 0.2f, true);
        EditorKit.Part("Ball", PrimitiveType.Sphere, hakka.transform, Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f),
            ToolMat("HakkaCloth", new Color(0.70f, 0.58f, 0.42f), 0.05f));
        var twine = ToolMat("Twine", new Color(0.80f, 0.72f, 0.55f), 0.1f);
        Trim("Twine", PrimitiveType.Cylinder, hakka.transform, Vector3.zero, new Vector3(0.123f, 0.004f, 0.123f), twine);
        Trim("Twine2", PrimitiveType.Cylinder, hakka.transform, Vector3.zero, new Vector3(0.123f, 0.004f, 0.123f), twine, new Vector3(90f, 0f, 0f));
        Trim("Knot", PrimitiveType.Sphere, hakka.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.03f, 0.03f, 0.03f), twine);
        Trim("Tuft", PrimitiveType.Cylinder, hakka.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.008f, 0.015f, 0.008f), twine);
        hakka.AddComponent<HakkaPatas>();
        return hakka.GetComponent<XRBaseInteractable>();
    }

    // Climbing: point at the ladder itself (no extra bar). Its poles rise about 0.9 m above the
    // floor (BlockoutBuilder), so from inside the hut you point at the ladder top to climb down,
    // and from the ground at the ladder to climb up. Each target is an invisible box round that
    // part of the ladder (big enough for the pointer to find easily); the ladder's poles light up
    // yellow while pointed at.
    static void BuildLadderHandles(Transform root)
    {
        var half = BlockoutBuilder.HutSize / 2f;
        float ladderZ = BlockoutBuilder.LadderZ;

        var groundSpot = EditorKit.Empty("GroundSpot", root, FarmLayout.OnGround(new Vector3(0f, 0f, -half - 2f)));
        var hutSpot = EditorKit.Empty("HutSpot", root, new Vector3(0f, FloorTop, 0.3f));

        // The ladder's shapes: the poles and top rung light up for climbing down, all of it for up.
        var topParts = new System.Collections.Generic.List<Renderer>();
        var allParts = new System.Collections.Generic.List<Renderer>();
        var ladder = GameObject.Find("WatchHut")?.transform.Find("Ladder");
        if (ladder != null)
            foreach (var r in ladder.GetComponentsInChildren<Renderer>())
            {
                allParts.Add(r);
                if (r.name != "Rung") topParts.Add(r);
            }
        else Debug.LogWarning("Night Watch: no WatchHut/Ladder found; the climb targets will not light up.");

        float above = BlockoutBuilder.LadderAboveFloor;
        var top = ClimbTarget("LadderTop", root, new Vector3(0f, FloorTop + above / 2f, ladderZ), new Vector3(0.8f, above + 0.1f, 0.4f));
        var down = top.AddComponent<LadderClimb>();
        down.goesDown = true;
        down.highlightColor = HandleHighlight;
        down.highlightRenderers = topParts.ToArray();
        down.destination = groundSpot;
        down.label = PointLabel("Climb down", root, new Vector3(0f, FloorTop + above + 0.15f, ladderZ),
            top.GetComponent<XRSimpleInteractable>()).GetComponent<TextMeshPro>();

        // From the ground: the ladder from knee height up to just under the floor.
        float floor = BlockoutBuilder.HutFloorHeight;
        var bottom = ClimbTarget("LadderBottom", root, new Vector3(0f, (0.3f + floor - 0.1f) / 2f, ladderZ - 0.05f),
            new Vector3(0.8f, floor - 0.4f, 0.45f));
        var up = bottom.AddComponent<LadderClimb>();
        up.goesDown = false;
        up.highlightColor = HandleHighlight;
        up.highlightRenderers = allParts.ToArray();
        up.destination = hutSpot;
        // Faces south, toward a player standing on the ground, at eye height beside the ladder.
        var upLabel = EditorKit.Text("Label_Climb up", root, new Vector3(0f, 1.75f, ladderZ - 0.3f), Quaternion.Euler(0f, 180f, 0f),
            new Vector2(0.6f, 0.12f), "Climb up", LabelSize, TextAlignmentOptions.Center);
        upLabel.gameObject.AddComponent<HoverLabel>().targets = new XRBaseInteractable[] { bottom.GetComponent<XRSimpleInteractable>() };
    }

    // An invisible box that the pointer can find, with the press logic added by the caller.
    static GameObject ClimbTarget(string name, Transform root, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider>().size = size;
        go.AddComponent<XRSimpleInteractable>();
        return go;
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
        // A wooden desk plate under the board and a stand from the floor up to it, so it does not
        // float (the old stand was 0.25 m short).
        var wood = EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f));
        var plate = EditorKit.Part("Desk", PrimitiveType.Cube, board.transform, boardCenter + boardRot * Vector3.forward * 0.04f,
            new Vector3(0.96f, 0.68f, 0.03f), wood, false);
        plate.transform.rotation = boardRot;
        EditorKit.Part("Stand", PrimitiveType.Cube, board.transform, new Vector3(-1.2f, FloorTop + 0.27f, 0.3f),
            new Vector3(0.1f, 0.54f, 0.5f), wood);
        // The desk and stand are solid: the player cannot walk through them.
        EditorKit.BodyWall("Solid", board.transform, new Vector3(-1.2f, FloorTop + 0.45f, 0.3f), Quaternion.identity,
            new Vector3(0.5f, 0.9f, 0.9f));
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
        // The results fill the board above the buttons.
        var results = EditorKit.Text("Results", debriefPanel.transform, center + new Vector3(0f, 0.35f, 0f), Quaternion.identity,
            new Vector2(5.8f, 2.7f), "", 1.4f);
        // The credits on their own board to the right, turned toward the hut: every third-party
        // asset with its author and licence did not fit beside the results.
        var creditsAt = center + new Vector3(5f, 0f, -1.2f);
        var creditsTurn = Quaternion.Euler(0f, 35f, 0f);
        EditorKit.Board("CreditsBackground", debriefPanel.transform, creditsAt, creditsTurn, new Vector2(3.6f, 3.6f), dark);
        var credits = EditorKit.Text("Credits", debriefPanel.transform, creditsAt, creditsTurn,
            new Vector2(3.3f, 3.3f), "", 1f);

        // Restart: a green button at the bottom of the results board. Reachable with the pointer
        // from the hut or from the ground (the hut may have collapsed).
        // At 3 m high, in front of the board's lower edge: seen from the hut over the north rail
        // (at 2.3 m the rail hid them).
        var restartPosition = new Vector3(-0.65f, 3f, 6.85f);
        var restart = EditorKit.Empty("Restart", debriefRoot.transform, restartPosition).gameObject;
        EditorKit.Part("Button", PrimitiveType.Cube, restart.transform, Vector3.zero, new Vector3(1f, 0.35f, 0.1f),
            EditorKit.ButtonMat("ButtonRestart", EditorKit.ButtonGreen));
        restart.AddComponent<XRSimpleInteractable>();
        restart.AddComponent<RestartButton>();
        EditorKit.ButtonLabel(EditorKit.Text("Label", restart.transform, restartPosition + new Vector3(0f, 0f, -0.07f), Quaternion.identity,
            new Vector2(0.9f, 0.3f), "RESTART", 1.8f, TextAlignmentOptions.Center));

        // Quit: closes the game (the Windows build is full screen). Esc does the same.
        var quitPosition = new Vector3(0.65f, 3f, 6.85f);
        var quit = EditorKit.Empty("Quit", debriefRoot.transform, quitPosition).gameObject;
        EditorKit.Part("Button", PrimitiveType.Cube, quit.transform, Vector3.zero, new Vector3(1f, 0.35f, 0.1f),
            EditorKit.ButtonMat("ButtonQuit", EditorKit.ButtonRed));
        quit.AddComponent<XRSimpleInteractable>();
        quit.AddComponent<QuitButton>();
        EditorKit.ButtonLabel(EditorKit.Text("Label", quit.transform, quitPosition + new Vector3(0f, 0f, -0.07f), Quaternion.identity,
            new Vector2(0.9f, 0.3f), "QUIT", 1.8f, TextAlignmentOptions.Center));

        var dp = debriefRoot.AddComponent<DebriefPanel>();
        dp.panel = debriefPanel;
        dp.results = results;
        dp.credits = credits;
        dp.restartButton = restart;
        dp.quitButton = quit;
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
        // 2.6 m south of the hut centre: the buttons are under 10 m away (the pointer's reach; at 4 m they were not).
        gm.debriefSpot = EditorKit.Empty("DebriefSpot", game, FarmLayout.OnGround(new Vector3(0f, 0f, -2.6f)));

        // When the hut collapses, the things inside it disappear with it.
        var damage = hut != null ? hut.GetComponent<HutDamage>() : null;
        if (damage != null)
        {
            var board = GameObject.Find("NoticeBoard");
            damage.hideOnCollapse = new[] { GameObject.Find("HutProps"), board };
            damage.followers = new[] { "HutProps/Table", "HutProps/FirePot", "HutProps/Basket" }
                .Select(path => GameObject.Find(path)?.transform)
                .Append(board != null ? board.transform : null)
                .Where(t => t != null).ToArray();
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
