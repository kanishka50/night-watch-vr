using UnityEngine;

// Where the farm's parts stand in the village (hut at the Unity origin, +z north), and the
// places kept clear of the village's plants: the yard round the hut, the paths from the start
// spot, round the bonfires and the neighbour's house, and the crop plots (planted with our own
// rice). Used by the village builder, the field tools and the ground jobs. The village's paddy
// plots are 9.25 x 8.75 m, with bunds (raised earth paths, about 1 m wide) along x = 0 and z = 0
// through the hut. See docs/village-port.md.
public static class FarmLayout
{
    public const float PlotX = 9.25f, PlotZ = 8.75f;

    // The 3 plots that elephants eat (x and y of each Rect are Unity x and z).
    public static readonly (string name, Rect area)[] CropPlots =
    {
        ("Plot_NorthWest", new Rect(-PlotX, 0f, PlotX, PlotZ)),
        ("Plot_SouthWest", new Rect(-PlotX, -PlotZ, PlotX, PlotZ)),
        ("Plot_SouthEast", new Rect(0f, -PlotZ, PlotX, PlotZ)),
    };

    // Beside each lane, so elephants walk past within reach of the fire.
    public static readonly (string side, Vector3 position)[] Bonfires =
    {
        ("east", new Vector3(22.5f, 0f, -4.5f)),
        ("west", new Vector3(-22.5f, 0f, -4.5f)),
        ("south", new Vector3(-13f, 0f, -23.5f)),
    };

    // East of the hut, in the yard, in view from the start spot.
    public static readonly Vector3 Energiser = new Vector3(2.8f, 0f, 1.2f);

    const float YardRadius = 4f, BonfireRadius = 3f, HouseRadius = 6.5f, PathHalfWidth = 0.8f;

    // Walking routes from the hut and the start spot, along the bunds where possible.
    static readonly Vector2[][] Paths =
    {
        new[] { new Vector2(0f, 0f), new Vector2(0f, -PlotZ) },
        new[] { new Vector2(0f, -PlotZ), new Vector2(22.5f, -PlotZ), new Vector2(22.5f, -4.5f) },
        new[] { new Vector2(0f, -PlotZ), new Vector2(-22.5f, -PlotZ), new Vector2(-22.5f, -4.5f) },
        new[] { new Vector2(0f, -PlotZ), new Vector2(0f, -3f * PlotZ), new Vector2(-13f, -3f * PlotZ), new Vector2(-13f, -23.5f) },
    };

    // Round open places: the yard, the bonfires and the neighbour's house.
    static (Vector2 centre, float radius)[] Circles()
    {
        var house = BlockoutBuilder.HouseCenter;
        var list = new (Vector2, float)[Bonfires.Length + 2];
        list[0] = (Vector2.zero, YardRadius);
        list[1] = (new Vector2(house.x, house.z), HouseRadius);
        for (int i = 0; i < Bonfires.Length; i++)
            list[i + 2] = (new Vector2(Bonfires[i].position.x, Bonfires[i].position.z), BonfireRadius);
        return list;
    }

    // True where the player walks or works: no village plants and none of our rice here.
    public static bool IsOpen(Vector3 p)
    {
        var q = new Vector2(p.x, p.z);
        foreach (var (centre, radius) in Circles())
            if ((q - centre).sqrMagnitude < radius * radius) return true;
        foreach (var path in Paths)
            for (int i = 0; i + 1 < path.Length; i++)
                if (DistanceToSegment(q, path[i], path[i + 1]) < PathHalfWidth) return true;
        return false;
    }

    public static bool InCropPlot(Vector3 p, float margin = 0f)
    {
        foreach (var (_, area) in CropPlots)
            if (p.x > area.xMin - margin && p.x < area.xMax + margin && p.z > area.yMin - margin && p.z < area.yMax + margin)
                return true;
        return false;
    }

    // Village plants (rice, grass, shrubs, small trees, rocks) are not drawn here.
    public static bool ClearOfPlants(Vector3 p) => IsOpen(p) || InCropPlot(p, 0.3f);

    // A flooded village plot (x and y of the Rect are Unity x and z) is drained when it is a crop
    // plot (ripe rice stands in a drained field) or when the yard, a bonfire or the house is on it.
    public static bool Drained(Rect plot)
    {
        var inner = Rect.MinMaxRect(plot.xMin + 0.1f, plot.yMin + 0.1f, plot.xMax - 0.1f, plot.yMax - 0.1f);
        foreach (var (_, area) in CropPlots)
            if (inner.Overlaps(area)) return true;
        foreach (var (centre, radius) in Circles())
        {
            var nearest = new Vector2(Mathf.Clamp(centre.x, inner.xMin, inner.xMax), Mathf.Clamp(centre.y, inner.yMin, inner.yMax));
            if ((nearest - centre).sqrMagnitude < radius * radius) return true;
        }
        return false;
    }

    // The same point standing on the village ground (the Terrain built by VillageBuilder).
    public static Vector3 OnGround(Vector3 p)
    {
        var go = GameObject.Find("Village/Terrain");
        var terrain = go != null ? go.GetComponent<Terrain>() : null;
        if (terrain == null) return p;
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return p;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }
}
