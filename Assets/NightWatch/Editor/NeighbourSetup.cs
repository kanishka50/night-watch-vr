using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the neighbour's side of the story (called by Night Watch > Build Full Game, after the
// NavMesh is baked):
//  - the door that opens, the neighbour (a simple figure with a lamp) and the rice store by the house
//  - the places they walk between (door, yard) and where the bull goes (the store)
//  - the phone on the hut's west rail, to call and warn the neighbour
//  - NeighbourHouse, which runs it all
// The house walls and window are built by BlockoutBuilder, the window light by NightLighting.
public static class NeighbourSetup
{
    public static void Build(Transform game)
    {
        var root = EditorKit.Group("Neighbour", game);
        var c = BlockoutBuilder.HouseCenter;
        float front = c.z + BlockoutBuilder.HouseSize / 2f;
        float doorX = c.x - 1f;

        // The door: a hinge on its left edge, the panel beside it. It opens inward.
        var hinge = EditorKit.Empty("DoorHinge", root, new Vector3(doorX - 0.45f, 0f, front + 0.05f));
        EditorKit.Part("Door", PrimitiveType.Cube, hinge, new Vector3(0.45f, 0.95f, 0f), new Vector3(0.9f, 1.9f, 0.05f),
            EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)), false);

        var doorPoint = EditorKit.Empty("DoorPoint", root, new Vector3(doorX, 0f, front + 0.3f));
        var yardPoint = EditorKit.Empty("YardPoint", root, new Vector3(doorX - 1.4f, 0f, front + 1.3f));
        var storePoint = EditorKit.Empty("StorePoint", root, new Vector3(c.x + BlockoutBuilder.HouseSize / 2f + 1.3f, 0f, c.z));

        // Rice sacks against the east wall: what the elephant comes for.
        var jute = EditorKit.Mat("RiceSack", new Color(0.62f, 0.52f, 0.34f));
        float wallX = c.x + BlockoutBuilder.HouseSize / 2f;
        EditorKit.Part("RiceSack", PrimitiveType.Cube, root, new Vector3(wallX + 0.3f, 0.25f, c.z - 0.5f), new Vector3(0.5f, 0.5f, 0.7f), jute, false);
        EditorKit.Part("RiceSack", PrimitiveType.Cube, root, new Vector3(wallX + 0.3f, 0.25f, c.z + 0.4f), new Vector3(0.5f, 0.5f, 0.7f), jute, false);
        EditorKit.Part("RiceSack", PrimitiveType.Cube, root, new Vector3(wallX + 0.3f, 0.7f, c.z), new Vector3(0.45f, 0.4f, 0.6f), jute, false);

        var figure = BuildFigure(root, doorPoint.position, out var lampPivot, out var lamp);

        var house = root.gameObject.AddComponent<NeighbourHouse>();
        house.door = hinge;
        var window = GameObject.Find("Blockout/NeighbourHouse/Window");
        if (window != null) house.window = window.GetComponent<Renderer>();
        var windowLight = GameObject.Find("NeighbourWindowLight");
        if (windowLight != null) house.windowLight = windowLight.GetComponent<Light>();
        house.figure = figure;
        house.lampPivot = lampPivot;
        house.lamp = lamp;
        house.doorPoint = doorPoint;
        house.yardPoint = yardPoint;
        house.storePoint = storePoint;

        BuildPhone();
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
        float railTop = BlockoutBuilder.HutFloorHeight + 0.5f + 0.45f;
        var position = new Vector3(-BlockoutBuilder.HutSize / 2f + 0.1f, railTop + 0.01f, -0.6f);

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
        EditorKit.Label("Phone: call your neighbour", hutProps.transform, position + new Vector3(0.15f, 0.28f, 0f));
    }
}
