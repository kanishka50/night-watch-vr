using UnityEngine;

// Where the farm's parts stand in the village (hut at the Unity origin, +z north), and the
// places kept clear of the village's plants: the yard round the hut, the paths from the start
// spot, round the bonfires, the neighbour's back yard, the strip under the fence, and the crop
// plots (planted with the village's rice). Used by the village builder, the field tools and the ground jobs. The village's paddy
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

    // On the two open sides (the fence guards north and west), beside the elephants' ways in:
    // east on the bund between the crops and the neighbour's garden, south on the jungle way.
    public static readonly (string side, Vector3 position)[] Bonfires =
    {
        ("east", new Vector3(13.9f, 0f, -PlotZ)),
        ("south", new Vector3(-13f, 0f, -23.5f)),
    };

    // Where the elephants come from (village-port.md): north from the tank (lake) shore, south
    // from the jungle, and the village side from behind the houses to the north-east.
    public static readonly Vector3 SpawnNorth = new Vector3(0f, 0f, 32f);
    public static readonly Vector3 SpawnSouth = new Vector3(-13f, 0f, -62f);
    public static readonly Vector3 SpawnVillage = new Vector3(38f, 0f, 42f);

    // The neighbour's house: the village's hut_c, 27 m east of our hut. Its front porch faces the
    // village (east); its back wall faces our hut. The neighbour's yard, evening fire, back door,
    // window and rice store are on the back, in view and in reach of the hut (about 23 m).
    public static readonly Vector3 NeighbourHouse = new Vector3(26.83f, 0f, -3.23f);
    public const float NeighbourHouseYaw = 82.02f;
    public const float NeighbourBackWall = -2.43f;   // local z of the back wall's outside (front is +z); the plinth reaches -2.65
    public const float NeighbourFloor = 0.35f;       // the house stands on a 0.35 m plinth

    // A point given in the house's own frame (x along the back wall, +z toward the front porch),
    // standing on the village ground.
    public static Vector3 AtNeighbour(float x, float z) =>
        OnGround(NeighbourHouse + Quaternion.Euler(0f, NeighbourHouseYaw, 0f) * new Vector3(x, 0f, z));

    // East of the hut, in the yard, in view from the start spot.
    public static readonly Vector3 Energiser = new Vector3(2.8f, 0f, 1.2f);

    // The electric fence: two lines of the village's wire_fence sections, meeting at the north-west
    // corner. The west line stands where the village had its own; the north line is new (toward the
    // tank). South (jungle) and east (village) are open. outside: the side elephants come from.
    public const float NorthFenceZ = 19.5f, WestFenceX = -58.5f;
    public static readonly (string name, Vector2 from, Vector2 to, int sections, Vector3 outside)[] FenceLines =
    {
        ("West", new Vector2(WestFenceX, -55.5f), new Vector2(WestFenceX, NorthFenceZ), 21, Vector3.left),
        ("North", new Vector2(WestFenceX, NorthFenceZ), new Vector2(18.5f, NorthFenceZ), 22, Vector3.forward),
    };

    // No heavy village trees (40,000+ triangles each) this close to the hut: from the hut they
    // would always be drawn in full. Two big trees north of the hut are left out by this.
    public const float NoHeavyTrees = 30f;

    const float YardRadius = 4f, BonfireRadius = 3f, NeighbourYardRadius = 4f, PathHalfWidth = 0.8f;
    // Farmers keep the ground under an electric fence clear: plants touching the wire drain the power.
    const float FenceStripHalfWidth = 1f;

    // Walking routes from the hut and the start spot, along the bunds where possible.
    static readonly Vector2[][] Paths =
    {
        new[] { new Vector2(0f, 0f), new Vector2(0f, -PlotZ) },
        new[] { new Vector2(0f, -PlotZ), new Vector2(13.9f, -PlotZ) },
        new[] { new Vector2(0f, -PlotZ), new Vector2(0f, -3f * PlotZ), new Vector2(-13f, -3f * PlotZ), new Vector2(-13f, -23.5f) },
    };

    // Round open places: the yard, the bonfires and the neighbour's back yard.
    static (Vector2 centre, float radius)[] Circles()
    {
        var yard = NeighbourHouse + Quaternion.Euler(0f, NeighbourHouseYaw, 0f) * new Vector3(0f, 0f, -5.5f);
        var list = new (Vector2, float)[Bonfires.Length + 3];
        list[0] = (Vector2.zero, YardRadius);
        list[1] = (new Vector2(yard.x, yard.z), NeighbourYardRadius);
        // The threshing floor's plot: harvested and dry (VillageExtras).
        list[2] = (new Vector2(VillageExtras.Kamatha.x, VillageExtras.Kamatha.z), 4.3f);
        for (int i = 0; i < Bonfires.Length; i++)
            list[i + 3] = (new Vector2(Bonfires[i].position.x, Bonfires[i].position.z), BonfireRadius);
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
        foreach (var line in FenceLines)
            if (DistanceToSegment(q, line.from, line.to) < FenceStripHalfWidth) return true;
        foreach (var (centre, radius) in VillageExtras.Clearings())
            if ((q - centre).sqrMagnitude < radius * radius) return true;
        return OnFootPath(q);
    }

    // On the footpath from the hut to the dagaba (VillageExtras).
    public static bool OnFootPath(Vector2 q)
    {
        var path = VillageExtras.FootPath;
        for (int i = 0; i + 1 < path.Length; i++)
            if (DistanceToSegment(q, path[i], path[i + 1]) < VillageExtras.FootPathHalfWidth) return true;
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
