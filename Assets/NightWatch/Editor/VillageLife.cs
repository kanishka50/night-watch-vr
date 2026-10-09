using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Life round the paddy (living-village.md, block D), all driven by the time of day and quiet when
// the player is far:
//  - egrets wading in the flooded plots by day, flying off to roost at dusk (or when disturbed);
//  - bats flitting over the field at dusk;
//  - fireflies at night along the canal, the tank shore, the shade tree and the plots by the hut;
//  - thin smoke from two village kitchens and the neighbour's house in the evening (cooking fires);
//  - a ring of clay oil lamps (pahan) lit round the dagaba at dusk, with one shared warm light;
//  - strings of Buddhist flags from the dagaba's spire, waving in the wind;
//  - frogs croaking near water at night, a village dog that barks when an elephant is near;
//  - a few leaves drifting down under the shade tree.
// The scarecrows' swaying and their tin clappers are built with the scarecrows (VillageExtras).
// The birds and bats are simple shapes (no download needed): from the hut they read as an egret
// and a bat.
public static class VillageLife
{
    // Egrets: where each stands (in flooded plots near the hut, not in the crop plots).
    static readonly Vector2[] EgretSpots =
    {
        new Vector2(-13.5f, 12.5f), new Vector2(-11.8f, 14.6f), new Vector2(-15.6f, 4.2f),
        new Vector2(-22.6f, 12.9f), new Vector2(13f, 4.6f), new Vector2(15.3f, 13.2f),
    };
    static readonly Vector3[] BatCentres =
    {
        new Vector3(0f, 0f, 10f), new Vector3(-10f, 0f, 0f), new Vector3(8f, 0f, -10f), new Vector3(-5f, 0f, -20f), new Vector3(15f, 0f, 5f),
    };
    // Village kitchens near the farm (the village's hut_kitchen copies) and the neighbour's house.
    static readonly (Vector3 at, float roof, float spread)[] Smokes =
    {
        (new Vector3(28.6f, 0f, -18.1f), 3.3f, 1.6f), (new Vector3(44.4f, 0f, 35.3f), 3.3f, 1.6f), (FarmLayout.NeighbourHouse, 4.1f, 2.4f),
    };

    // Times (GameManager.DayProgress): dusk begins at about 0.22, night at 0.31, dawn at 0.96.
    const float EveningFrom = 0.15f, EveningTo = 0.45f;
    const float LampsFrom = 0.215f, LampsTo = 0.62f;
    const float NightFrom = 0.27f, NightTo = 0.965f;

    public static void Build(Transform root)
    {
        var life = EditorKit.Group("Life", root);
        BuildEgrets(life);
        BuildBats(life);
        BuildFireflies(life);
        BuildSmoke(life);
        BuildLamps(life);
        BuildFlags(life);
        BuildSounds(life);
        BuildLeaves(life);
    }

    // ---------------- egrets ----------------

    static void BuildEgrets(Transform life)
    {
        var group = EditorKit.Group("Egrets", life);
        var white = EditorKit.Mat("EgretWhite", new Color(0.92f, 0.92f, 0.89f));
        var beak = EditorKit.Mat("EgretBeak", new Color(0.9f, 0.72f, 0.2f));
        var leg = EditorKit.Mat("EgretLeg", new Color(0.08f, 0.08f, 0.08f));
        for (int i = 0; i < EgretSpots.Length; i++)
        {
            var at = FarmLayout.OnGround(new Vector3(EgretSpots[i].x, 0f, EgretSpots[i].y));
            var bird = EditorKit.Empty("Egret", group, at);
            bird.rotation = Quaternion.Euler(0f, i * 67f, 0f);
            var egret = bird.gameObject.AddComponent<Egret>();

            // Legs, hinged at the hip.
            egret.leftLeg = Pivot("LeftLeg", bird, new Vector3(-0.04f, 0.42f, 0f));
            egret.rightLeg = Pivot("RightLeg", bird, new Vector3(0.04f, 0.42f, 0f));
            Shape("Leg", PrimitiveType.Cylinder, egret.leftLeg, new Vector3(0f, -0.21f, 0f), new Vector3(0.015f, 0.21f, 0.015f), leg);
            Shape("Leg", PrimitiveType.Cylinder, egret.rightLeg, new Vector3(0f, -0.21f, 0f), new Vector3(0.015f, 0.21f, 0.015f), leg);
            // The body, a little nose-up, and the tail.
            var body = Shape("Body", PrimitiveType.Sphere, bird, new Vector3(0f, 0.5f, 0f), new Vector3(0.16f, 0.17f, 0.42f), white);
            body.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            // The long neck (an S from the shoulders), the head and the yellow dagger beak.
            egret.neck = Pivot("Neck", bird, new Vector3(0f, 0.55f, 0.15f));
            var lower = Shape("NeckLow", PrimitiveType.Cylinder, egret.neck, new Vector3(0f, 0.09f, 0.03f), new Vector3(0.04f, 0.1f, 0.04f), white);
            lower.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            var upper = Shape("NeckHigh", PrimitiveType.Cylinder, egret.neck, new Vector3(0f, 0.24f, 0.02f), new Vector3(0.035f, 0.08f, 0.035f), white);
            upper.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            Shape("Head", PrimitiveType.Sphere, egret.neck, new Vector3(0f, 0.33f, 0.04f), new Vector3(0.06f, 0.06f, 0.085f), white);
            Shape("Beak", PrimitiveType.Cube, egret.neck, new Vector3(0f, 0.325f, 0.14f), new Vector3(0.016f, 0.016f, 0.13f), beak);
            // Wings, hinged at the shoulders: folded (short) when standing, spread to fly.
            egret.leftWing = Pivot("LeftWing", bird, new Vector3(-0.07f, 0.56f, 0.04f));
            egret.rightWing = Pivot("RightWing", bird, new Vector3(0.07f, 0.56f, 0.04f));
            Shape("Wing", PrimitiveType.Cube, egret.leftWing, new Vector3(-0.38f, 0f, -0.06f), new Vector3(0.76f, 0.015f, 0.3f), white);
            Shape("Wing", PrimitiveType.Cube, egret.rightWing, new Vector3(0.38f, 0f, -0.06f), new Vector3(0.76f, 0.015f, 0.3f), white);
            LearnSetup.Spot(bird, "kokka", at + Vector3.up * 0.5f, 0.6f);
            // They leave one after another at the end of the afternoon.
            egret.leaveAt = 0.205f + i * 0.005f;
        }
    }

    // ---------------- bats ----------------

    static void BuildBats(Transform life)
    {
        var group = EditorKit.Group("Bats", life);
        var dark = EditorKit.Mat("BatDark", new Color(0.07f, 0.06f, 0.06f));
        for (int i = 0; i < BatCentres.Length; i++)
        {
            var bat = EditorKit.Empty("Bat", group, BatCentres[i] + Vector3.up * 6f);
            var b = bat.gameObject.AddComponent<Bat>();
            b.centre = BatCentres[i];
            b.radius = 10f + 1.5f * i;
            Shape("Body", PrimitiveType.Sphere, bat, Vector3.zero, new Vector3(0.05f, 0.045f, 0.1f), dark);
            b.leftWing = Pivot("LeftWing", bat, new Vector3(-0.02f, 0f, 0f));
            b.rightWing = Pivot("RightWing", bat, new Vector3(0.02f, 0f, 0f));
            Shape("Wing", PrimitiveType.Cube, b.leftWing, new Vector3(-0.09f, 0f, 0f), new Vector3(0.18f, 0.005f, 0.09f), dark);
            Shape("Wing", PrimitiveType.Cube, b.rightWing, new Vector3(0.09f, 0f, 0f), new Vector3(0.18f, 0.005f, 0.09f), dark);
        }
    }

    // ---------------- fireflies ----------------

    static void BuildFireflies(Transform life)
    {
        var group = EditorKit.Group("Fireflies", life);
        var window = group.gameObject.AddComponent<TimeWindow>();
        window.from = NightFrom;
        window.to = NightTo;
        window.maxDistance = 400f;   // all four swarms are near the hut
        Fireflies(group, "Canal", new Vector3(-9f, 0.9f, 23f), new Vector3(5f, 1.2f, 12f), 5f);
        Fireflies(group, "TankShore", new Vector3(-7f, 1.2f, 41f), new Vector3(14f, 1.5f, 3f), 5f);
        Fireflies(group, "ShadeTree", VillageExtras.ShadeTree + Vector3.up * 1.6f, new Vector3(7f, 2.5f, 7f), 6f);
        Fireflies(group, "HutPlots", new Vector3(-8f, 0.9f, 6f), new Vector3(7f, 1f, 6f), 4f);
    }

    static void Fireflies(Transform parent, string name, Vector3 at, Vector3 box, float rate)
    {
        var go = new GameObject("Fireflies_" + name);
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
        main.startColor = new Color(0.75f, 1f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        // Slow drifting flight.
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.2f;
        // Each one blinks: a few soft flashes in its life.
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0f, 0.2f),
                new GradientAlphaKey(0f, 0.4f), new GradientAlphaKey(1f, 0.48f), new GradientAlphaKey(0f, 0.6f),
                new GradientAlphaKey(0f, 0.78f), new GradientAlphaKey(0.9f, 0.86f), new GradientAlphaKey(0f, 1f),
            });
        colour.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = GlowMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 0.02f;   // stay small dots even up close
    }

    // Additive glow with the soft dot (like FireEffect's flames), for the fireflies.
    static Material GlowMaterial()
    {
        const string path = "Assets/NightWatch/Materials/GlowParticle.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", FireEffect.SoftDot());
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);   // additive
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------------- evening smoke ----------------

    static void BuildSmoke(Transform life)
    {
        var group = EditorKit.Group("Smoke", life);
        foreach (var (at, roof, spread) in Smokes)
        {
            var ground = FarmLayout.OnGround(at);
            var go = new GameObject("KitchenSmoke");
            go.transform.SetParent(group, false);
            go.transform.position = ground + Vector3.up * roof;
            var window = go.AddComponent<TimeWindow>();
            window.from = EveningFrom;
            window.to = EveningTo;
            window.fade = 0.03f;
            window.maxDistance = 160f;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;
            var emission = ps.emission;
            emission.rateOverTime = 3f;
            var shape = ps.shape;
            // Smoke seeps out through the thatch (village kitchens have no chimney).
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spread, 0.2f, spread);
            // It drifts with the north-east wind as it rises.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.35f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.35f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 4f)));
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.8f, 0.82f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.3f, 0.15f), new GradientAlphaKey(0.15f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = WaterSplash.Material();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // ---------------- the dagaba's oil lamps and flags ----------------

    static void BuildLamps(Transform life)
    {
        const int count = 24;
        const float ringRadius = 4.4f;
        var centre = FarmLayout.OnGround(VillageExtras.Dagaba);
        var group = EditorKit.Empty("PahanLamps", life, centre);
        var clay = EditorKit.Mat("ClayPot", new Color(0.62f, 0.33f, 0.20f));
        var flameMat = EditorKit.Mat("LampFlame", new Color(1f, 0.72f, 0.3f), true);
        // The flames and their light switch on and off together; the clay cups stay all day.
        var lit = EditorKit.Empty("Lit", group, centre);
        var flames = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            var spot = FarmLayout.OnGround(centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * ringRadius);
            var local = group.InverseTransformPoint(spot);
            Shape("Lamp", PrimitiveType.Cylinder, group, local + Vector3.up * 0.025f, new Vector3(0.11f, 0.025f, 0.11f), clay).isStatic = true;
            var pivot = Pivot("Flame", lit, local + Vector3.up * 0.05f);
            Shape("FlameShape", PrimitiveType.Sphere, pivot, new Vector3(0f, 0.035f, 0f), new Vector3(0.022f, 0.06f, 0.022f), flameMat);
            flames[i] = pivot;
        }
        var lightGo = new GameObject("LampGlow");
        lightGo.transform.SetParent(lit, false);
        lightGo.transform.localPosition = Vector3.up * 0.6f;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.66f, 0.32f);
        light.range = 7.5f;
        light.intensity = 1.6f;
        light.shadows = LightShadows.None;

        var flicker = lit.gameObject.AddComponent<FlameFlicker>();
        flicker.flames = flames;
        flicker.glow = light;
        var window = lit.gameObject.AddComponent<TimeWindow>();
        window.from = LampsFrom;
        window.to = LampsTo;
        window.hideWhenOff = true;
        window.maxDistance = 200f;
    }

    // Four strings of flags from the spire to bamboo poles, in the five colours of the Buddhist flag.
    static void BuildFlags(Transform life)
    {
        var ground = FarmLayout.OnGround(VillageExtras.Dagaba);
        float top = ground.y + 6f;
        var stupa = GameObject.Find("Village/Buildings/stupa");
        if (stupa != null)
        {
            var renderers = stupa.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                top = b.max.y - 0.4f;
            }
        }
        var group = EditorKit.Empty("Flags", life, ground);
        var pole = EditorKit.Mat("HutPole", new Color(0.40f, 0.31f, 0.22f));
        var colours = new[]
        {
            FlagMat("FlagBlue", new Color(0.1f, 0.3f, 0.75f)), FlagMat("FlagYellow", new Color(0.95f, 0.8f, 0.1f)),
            FlagMat("FlagRed", new Color(0.75f, 0.12f, 0.1f)), FlagMat("FlagWhite", new Color(0.93f, 0.93f, 0.9f)),
            FlagMat("FlagOrange", new Color(0.95f, 0.5f, 0.1f)),
        };
        var stringMat = EditorKit.Mat("FlagString", new Color(0.9f, 0.9f, 0.85f));
        var pivots = new List<Transform>();
        var spire = new Vector3(VillageExtras.Dagaba.x, top, VillageExtras.Dagaba.z);
        foreach (float bearing in new[] { 45f, 135f, 225f, 315f })
        {
            float a = bearing * Mathf.Deg2Rad;
            var foot = FarmLayout.OnGround(VillageExtras.Dagaba + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 9f);
            const float poleHeight = 3.2f;
            Shape("FlagPole", PrimitiveType.Cylinder, group, group.InverseTransformPoint(foot) + Vector3.up * (poleHeight / 2f),
                new Vector3(0.06f, poleHeight / 2f, 0.06f), pole).isStatic = true;
            var end = foot + Vector3.up * (poleHeight - 0.1f);

            // The string sags in the middle.
            var line = new GameObject("FlagString").AddComponent<LineRenderer>();
            line.transform.SetParent(group, false);
            line.useWorldSpace = true;
            line.positionCount = 14;
            line.widthMultiplier = 0.012f;
            line.sharedMaterial = stringMat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float length = Vector3.Distance(spire, end);
            Vector3 At(float t) => Vector3.Lerp(spire, end, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * length * 0.06f);
            for (int i = 0; i < line.positionCount; i++) line.SetPosition(i, At(i / (float)(line.positionCount - 1)));

            var along = end - spire;
            var right = new Vector3(along.x, 0f, along.z).normalized;
            var facing = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
            int flags = Mathf.FloorToInt(length / 0.42f);
            for (int k = 1; k < flags; k++)
            {
                float t = k / (float)flags;
                var pivot = new GameObject("Flag").transform;
                pivot.SetParent(group, false);
                pivot.position = At(t);
                pivot.rotation = facing;
                Shape("Cloth", PrimitiveType.Quad, pivot, new Vector3(0f, -0.15f, 0f), new Vector3(0.24f, 0.3f, 1f), colours[k % colours.Length]);
                pivots.Add(pivot);
            }
        }
        group.gameObject.AddComponent<FlagWave>().flags = pivots.ToArray();
    }

    static Material FlagMat(string name, Color colour)
    {
        var mat = EditorKit.Mat(name, colour);
        mat.SetColor("_BaseColor", colour);
        mat.SetFloat("_Cull", 0f);   // cloth: seen from both sides
        mat.SetFloat("_Smoothness", 0.1f);
        mat.doubleSidedGI = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------- sounds ----------------

    static void BuildSounds(Transform life)
    {
        var group = EditorKit.Group("Sounds", life);
        // Frogs near water at night: the plots by the canal's end and the tank shore.
        foreach (var at in new[] { new Vector3(-14f, 0f, 11f), new Vector3(-10f, 0f, 41.5f), new Vector3(14f, 0f, 9f) })
        {
            var go = EditorKit.Empty("Frogs", group, FarmLayout.OnGround(at) + Vector3.up * 0.2f);
            var source = go.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0.4f;
            go.gameObject.AddComponent<LoopSound>().clip = LoopSound.Clip.Frogs;
            var window = go.gameObject.AddComponent<TimeWindow>();
            window.from = NightFrom;
            window.to = NightTo;
            window.fade = 0.03f;
            window.maxDistance = 60f;
        }
        // (No village dog: its barking was taken out, 10 Oct 2026.)
    }

    // ---------------- falling leaves ----------------

    static void BuildLeaves(Transform life)
    {
        var go = new GameObject("FallingLeaves");
        go.transform.SetParent(life, false);
        go.transform.position = FarmLayout.OnGround(VillageExtras.ShadeTree) + Vector3.up * 6f;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 14f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.6f, 0.2f), new Color(0.7f, 0.5f, 0.2f));
        main.gravityModifier = 0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12;
        var emission = ps.emission;
        emission.rateOverTime = 0.4f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(7f, 1.5f, 7f);
        // Tumbling and drifting downwind as they fall.
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.3f;
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = WaterSplash.Material();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // ---------------- helpers ----------------

    static Transform Pivot(string name, Transform parent, Vector3 local)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = local;
        return t;
    }

    static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 local, Vector3 size, Material mat)
    {
        var go = EditorKit.Part(name, type, parent, local, size, mat, false);
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }
}
