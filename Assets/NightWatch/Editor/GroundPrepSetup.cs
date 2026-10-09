using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the afternoon jobs on the ground (called by Night Watch > Build Full Game, after the
// NavMesh is baked, so these objects do not change the baked map):
//  - two bonfires at the open sides (east and south)
//  - the fence energiser east of the hut, facing the start, with a wire on sticks to the fence
// Where they stand is in FarmLayout; each is set on the village ground.
//  - the task board by the ladder and the player's start spot
public static class GroundPrepSetup
{
    // Where the player starts: on the path south of the hut, facing north.
    public static readonly Vector3 StartPosition = new Vector3(0f, 0f, -7f);

    public static void Build(Transform game)
    {
        var root = EditorKit.Group("GroundPrep", game);

        foreach (var (side, position) in FarmLayout.Bonfires)
            BuildBonfire(root, side, FarmLayout.OnGround(position));

        BuildEnergiser(root, FarmLayout.OnGround(FarmLayout.Energiser));
        var board = BuildTaskBoard(root, FarmLayout.OnGround(new Vector3(-1.7f, 0f, -2.8f)) + Vector3.up * 1.5f);

        var start = EditorKit.Empty("StartSpot", root, FarmLayout.OnGround(StartPosition));
        var prep = root.gameObject.AddComponent<GroundPrep>();
        prep.startSpot = start;
        prep.tasks = board.tasks;
        prep.alert = board.alert;
    }

    static void BuildBonfire(Transform root, string side, Vector3 position)
    {
        var go = new GameObject("Bonfire_" + side);
        go.transform.SetParent(root, false);
        go.transform.position = position;

        // One big box to point at, easier to hit than the thin logs.
        var target = go.AddComponent<BoxCollider>();
        target.center = new Vector3(0f, 0.6f, 0f);
        target.size = new Vector3(1.4f, 1.2f, 1.4f);

        // Elephants walk round the fire, not through it.
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = 0.9f;
        obstacle.height = 2f;
        obstacle.center = new Vector3(0f, 1f, 0f);
        obstacle.carving = true;

        EditorKit.Part("Ash", PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.01f, 0f),
            new Vector3(1.6f, 0.01f, 1.6f), EditorKit.Mat("Ash", new Color(0.18f, 0.16f, 0.15f)), false);

        // Four logs leaning together like a small tent.
        var wood = EditorKit.Mat("Firewood", new Color(0.40f, 0.27f, 0.15f));
        var logs = new Renderer[4];
        for (int i = 0; i < 4; i++)
        {
            float yaw = i * 90f + 45f;
            var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-25f, 0f, 0f);
            var log = EditorKit.Part("Log", PrimitiveType.Cylinder, go.transform,
                rotation * Vector3.forward * 0.3f + Vector3.up * 0.55f, new Vector3(0.14f, 0.65f, 0.14f), wood, false);
            log.transform.localRotation = rotation;
            logs[i] = log.GetComponent<Renderer>();
        }

        var flame = new GameObject("Flame");
        flame.transform.SetParent(go.transform, false);
        flame.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        // The flames start low among the logs and rise well above them (about 1.5 m), as a
        // night watch fire of this size burns.
        FireEffect.Build(flame.transform, new Vector3(0f, -0.55f, 0f), 2.5f);

        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(flame.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.25f);
        light.range = 16f;
        light.intensity = 5f;
        light.shadows = LightShadows.None;

        var crackle = flame.AddComponent<AudioSource>();
        crackle.spatialBlend = 1f;
        crackle.minDistance = 2f;
        crackle.maxDistance = 35f;
        crackle.volume = 0.7f;

        go.AddComponent<XRSimpleInteractable>();
        var bonfire = go.AddComponent<Bonfire>();
        bonfire.side = side;
        bonfire.flame = flame;
        bonfire.fireLight = light;
        bonfire.highlightRenderers = logs;

        // A big label, turned so it reads from the hut side.
        var outward = new Vector3(position.x, 0f, position.z).normalized;
        EditorKit.Text("Label", go.transform, position + Vector3.up * 2.4f, Quaternion.LookRotation(outward),
            new Vector2(2f, 0.4f), $"Bonfire ({side})", 2f, TextAlignmentOptions.Center);
    }

    static void BuildEnergiser(Transform root, Vector3 position)
    {
        var go = new GameObject("FenceEnergiser");
        go.transform.SetParent(root, false);
        go.transform.position = position;

        var target = go.AddComponent<BoxCollider>();
        target.center = new Vector3(0f, 0.75f, 0f);
        target.size = new Vector3(0.6f, 1.5f, 0.5f);

        EditorKit.Part("Post", PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.1f, 0.5f, 0.1f),
            EditorKit.Mat("FencePost", new Color(0.35f, 0.28f, 0.20f)), false);
        // The box faces south (-Z), toward the player at the start.
        var box = EditorKit.Part("Box", PrimitiveType.Cube, go.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.45f, 0.36f, 0.2f),
            EditorKit.Mat("EnergiserBox", new Color(0.45f, 0.55f, 0.45f)), false);
        var lamp = EditorKit.Part("Lamp", PrimitiveType.Sphere, go.transform, new Vector3(-0.11f, 1.18f, -0.1f), new Vector3(0.08f, 0.08f, 0.08f),
            EditorKit.Mat("EnergiserLamp", Color.white, true), false);
        EditorKit.Part("Switch", PrimitiveType.Cube, go.transform, new Vector3(0.1f, 1.06f, -0.11f), new Vector3(0.06f, 0.12f, 0.05f),
            EditorKit.Mat("Switch", new Color(0.08f, 0.08f, 0.08f)), false);

        // The lead-out wire to the north fence, held up on short sticks (it crosses a flooded plot,
        // so it does not lie on the ground). It ends where the bund x = 0 meets the fence, which
        // keeps it clear of the haystack at (3.4, 12.8).
        const float wireHeight = 1.2f;
        const int sticks = 4;
        var end = new Vector3(-position.x, 0f, FarmLayout.NorthFenceZ - 0.05f - position.z);
        var dir = end.normalized;
        var start = dir * 0.12f;
        var wire = EditorKit.Part("LeadOutWire", PrimitiveType.Cube, go.transform, (start + end) / 2f + Vector3.up * wireHeight,
            new Vector3(0.015f, 0.015f, (end - start).magnitude), EditorKit.Mat("Cable", new Color(0.05f, 0.05f, 0.05f)), false);
        wire.transform.localRotation = Quaternion.LookRotation(dir);
        var stickMat = EditorKit.Mat("FencePost", new Color(0.35f, 0.28f, 0.20f));
        for (int i = 1; i <= sticks; i++)
        {
            var at = end * i / (sticks + 1f);
            float ground = FarmLayout.OnGround(position + at).y - position.y;
            float height = wireHeight - ground + 0.05f;
            EditorKit.Part("WireStick", PrimitiveType.Cylinder, go.transform, new Vector3(at.x, ground + height / 2f, at.z),
                new Vector3(0.05f, height / 2f, 0.05f), stickMat, false);
        }

        var tick = go.AddComponent<AudioSource>();
        tick.spatialBlend = 1f;
        tick.minDistance = 1f;
        tick.maxDistance = 15f;
        tick.volume = 0.6f;

        go.AddComponent<XRSimpleInteractable>();
        var energiser = go.AddComponent<FenceEnergiser>();
        energiser.lamp = lamp.GetComponent<Renderer>();
        energiser.tickSource = tick;
        energiser.highlightRenderers = new[] { box.GetComponent<Renderer>() };

        // Reads from the start spot, looking north at the front of the box.
        EditorKit.Text("Label", go.transform, position + new Vector3(0f, 1.7f, 0f), Quaternion.identity,
            new Vector2(1.6f, 0.3f), "Fence energiser", 1.6f, TextAlignmentOptions.Center);
    }

    struct Board
    {
        public TextMeshPro tasks;
        public TextMeshPro alert;
    }

    // Faces south, toward a player standing on the path.
    static Board BuildTaskBoard(Transform root, Vector3 center)
    {
        var go = new GameObject("TaskBoard");
        go.transform.SetParent(root, false);
        var rotation = Quaternion.identity;

        EditorKit.Board("Background", go.transform, center, rotation, new Vector2(1.5f, 0.95f), new Color(0.07f, 0.08f, 0.10f));
        // A wooden back, so the text does not show mirrored from the hut.
        EditorKit.Part("Back", PrimitiveType.Cube, go.transform, center + Vector3.forward * 0.04f, new Vector3(1.56f, 1f, 0.03f),
            EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)), false);
        float ground = center.y - 1.5f;
        EditorKit.Part("Post", PrimitiveType.Cube, go.transform, new Vector3(center.x, ground + (1.5f - 0.47f) / 2f, center.z + 0.05f),
            new Vector3(0.08f, 1.5f - 0.47f, 0.08f), EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)), false);

        var tasks = EditorKit.Text("Tasks", go.transform, center + Vector3.up * 0.09f, rotation, new Vector2(1.4f, 0.7f), "", 1f);
        var alert = EditorKit.Text("Alert", go.transform, center - Vector3.up * 0.38f, rotation, new Vector2(1.4f, 0.14f), "", 0.8f);
        alert.color = new Color(1f, 0.85f, 0.3f);
        return new Board { tasks = tasks, alert = alert };
    }
}
