using System.Collections.Generic;
using UnityEngine;

// One crop plot of the paddy field. Elephants standing in it eat it. Like real elephants they
// eat and trample a patch: the rice clumps nearest to the elephant lie flat and turn brown
// (CropDrawer), so from the hut you can see where they fed. The average of all plots is the
// "crop saved" score.
[RequireComponent(typeof(BoxCollider))]
public class CropPlot : MonoBehaviour
{
    public static readonly List<CropPlot> All = new List<CropPlot>();

    [Range(0f, 100f)] public float health = 100f;
    [Tooltip("Crop lost per second by one adult elephant eating here.")]
    public float eatPerSecond = 0.35f;
    [Tooltip("Draws the rice clumps; eaten clumps lie flat and brown.")]
    public CropDrawer drawer;

    BoxCollider area;

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

    // An elephant eating for this many seconds, its mouth at this point.
    public void Eat(float seconds, Vector3 mouth)
    {
        if (health <= 0f) return;
        health = Mathf.Max(0f, health - eatPerSecond * seconds);
        if (drawer == null) return;
        int target = Mathf.RoundToInt((1f - health / 100f) * drawer.Count);
        while (drawer.TrampledCount < target && drawer.TrampleNearest(mouth)) { }
    }

    [ContextMenu("Test: an elephant eats 25% at a random spot")]
    void TestEat() => Eat(25f / eatPerSecond, RandomPoint());

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
