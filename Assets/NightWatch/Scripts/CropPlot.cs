using System.Collections.Generic;
using UnityEngine;

// One part of the paddy field. Elephants standing in it eat it, and the crop visibly
// shrinks and turns brown. The average of all plots is the "crop saved" score.
[RequireComponent(typeof(BoxCollider))]
public class CropPlot : MonoBehaviour
{
    public static readonly List<CropPlot> All = new List<CropPlot>();

    [Range(0f, 100f)] public float health = 100f;
    [Tooltip("Crop lost per second by one adult elephant eating here.")]
    public float eatPerSecond = 0.35f;
    [Tooltip("The green crop shape that shrinks as it is eaten.")]
    public Transform crop;
    public Color fullColor = new Color(0.36f, 0.55f, 0.20f);
    public Color eatenColor = new Color(0.35f, 0.27f, 0.16f);

    BoxCollider area;
    Renderer cropRenderer;
    MaterialPropertyBlock block;
    Vector3 fullScale;
    Vector3 fullPosition;

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
        if (crop != null)
        {
            cropRenderer = crop.GetComponent<Renderer>();
            fullScale = crop.localScale;
            fullPosition = crop.localPosition;
        }
        block = new MaterialPropertyBlock();
        UpdateLook();
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Eat(float seconds)
    {
        if (health <= 0f) return;
        health = Mathf.Max(0f, health - eatPerSecond * seconds);
        UpdateLook();
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

    void UpdateLook()
    {
        if (crop == null) return;
        float k = health / 100f;
        var s = fullScale;
        s.y = Mathf.Max(0.02f, fullScale.y * k);
        crop.localScale = s;
        // Keep the bottom on the ground while the crop gets shorter.
        crop.localPosition = fullPosition - new Vector3(0f, (fullScale.y - s.y) / 2f, 0f);
        if (cropRenderer != null)
        {
            block.SetColor("_BaseColor", Color.Lerp(eatenColor, fullColor, k));
            cropRenderer.SetPropertyBlock(block);
        }
    }
}
