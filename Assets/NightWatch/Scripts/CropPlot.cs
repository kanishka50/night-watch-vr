using System.Collections.Generic;
using UnityEngine;

// One crop plot of the paddy field. Like real elephants, they lose the farmer rice two ways:
//  - the trunk eats: while feeding, the clump nearest the mouth is gone (at eatPerSecond);
//  - the feet flatten: any elephant walking or standing in the plot flattens the clumps under
//    its four feet (they lie over and turn brown).
// Health is the share of clumps still standing, so both count. From the hut you see the gaps
// and the flat patches where they went (CropDrawer). The average of all plots is the "crop
// saved" score.
[RequireComponent(typeof(BoxCollider))]
public class CropPlot : MonoBehaviour
{
    public static readonly List<CropPlot> All = new List<CropPlot>();

    [Tooltip("Percent of the rice still standing (read only in play).")]
    [Range(0f, 100f)] public float health = 100f;
    [Tooltip("Crop eaten per second (percent of the plot) by one adult elephant feeding here.")]
    public float eatPerSecond = 0.35f;
    [Tooltip("Rice within this distance (m) of a foot is flattened. 0.25 = walking straight across a plot flattens about 1 clump in 10.")]
    public float footRadius = 0.25f;
    [Tooltip("Draws the rice clumps; eaten clumps lie flat and brown.")]
    public CropDrawer drawer;

    BoxCollider area;
    float eatenSoFar;   // clumps' worth eaten, not yet taken

    public static float AverageHealth
    {
        get
        {
            if (All.Count == 0) return 100f;
            float sum = 0f;
            foreach (var p in All) sum += p.health;
            return sum / All.Count;
        }
    }

    void Awake()
    {
        area = GetComponent<BoxCollider>();
        area.isTrigger = true;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // An elephant feeding for this many seconds, its mouth at this point.
    public void Eat(float seconds, Vector3 mouth)
    {
        if (health <= 0f) return;
        if (drawer == null || drawer.Count == 0)
        {
            health = Mathf.Max(0f, health - eatPerSecond * seconds);
            return;
        }
        eatenSoFar += eatPerSecond / 100f * drawer.Count * seconds;
        while (eatenSoFar >= 1f && drawer.EatNearest(mouth)) eatenSoFar -= 1f;
        UpdateHealth();
    }

    // An elephant's body in the plot: its four feet flatten the rice under them.
    // The feet are at the stand-in's legs (0.6 m to each side, 1 m forward and back), scaled with it.
    public void TrampleUnder(Transform elephant)
    {
        if (drawer == null || health <= 0f) return;
        int flattened = 0;
        for (int side = -1; side <= 1; side += 2)
            for (int end = -1; end <= 1; end += 2)
                flattened += drawer.FlattenAround(elephant.TransformPoint(0.6f * side, 0f, 1f * end), footRadius * elephant.localScale.x);
        if (flattened > 0) UpdateHealth();
    }

    void UpdateHealth() => health = 100f * drawer.StandingCount / drawer.Count;

    [ContextMenu("Test: an elephant eats 25% at a random spot")]
    void TestEat() => Eat(25f / eatPerSecond, RandomPoint());

    [ContextMenu("Test: an elephant walks across the middle (south to north)")]
    void TestWalk()
    {
        var b = area.bounds;
        var walker = new GameObject("TestWalker").transform;
        for (float z = b.min.z; z <= b.max.z; z += 0.2f)
        {
            walker.position = new Vector3(b.center.x, 0f, z);
            TrampleUnder(walker);
        }
        Destroy(walker.gameObject);
    }

    // True if the point is inside this plot (height ignored).
    public bool Contains(Vector3 point)
    {
        var b = area.bounds;
        return point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z;
    }

    public Vector3 RandomPoint()
    {
        var b = area.bounds;
        return new Vector3(Random.Range(b.min.x + 1f, b.max.x - 1f), 0f, Random.Range(b.min.z + 1f, b.max.z - 1f));
    }

    public static CropPlot At(Vector3 point)
    {
        foreach (var p in All)
            if (p.Contains(point)) return p;
        return null;
    }

    // The closest plot that still has crop left.
    public static CropPlot NearestWithCrop(Vector3 point)
    {
        CropPlot best = null;
        float bestDistance = float.MaxValue;
        foreach (var p in All)
        {
            if (p.health <= 0f) continue;
            float d = Vector3.Distance(point, p.area.bounds.center);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = p;
            }
        }
        return best;
    }
}
