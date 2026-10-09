using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the neighbour's side of the story (called by Night Watch > Build Full Game, after the
// NavMesh is baked), on the back of the village house hut_c (FarmLayout.NeighbourHouse), the
// side that faces our hut:
//  - a back door that opens and a window that glows, the bottle lamp by the door, the neighbour
//    (a simple figure with a lamp), the evening fire in the back yard and the rice store
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

        var figure = BuildFigure(root, doorPoint.position, out var lampPivot, out var lamp);

        var house = root.gameObject.AddComponent<NeighbourHouse>();
        house.door = hinge;
        house.window = window.GetComponent<Renderer>();
        house.figure = figure;
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
    static Transform BuildFigure(Transform root, Vector3 at, out Transform lampPivot, out Light lamp)
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
