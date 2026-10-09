using UnityEngine;
using UnityEngine.Rendering;

// Draws the rice clumps of one crop plot (the village's rice_gold model, on the village's own
// clump spots) with GPU instancing: one draw for the standing clumps and one for the trampled
// ones. Elephants eat and trample a patch, not the whole plot evenly, so CropPlot flattens the
// clumps nearest to the elephant: they lie over and turn brown. Made by ElephantSetup.
[ExecuteAlways]
public class CropDrawer : MonoBehaviour
{
    [Tooltip("The rice model's parts (mesh and material).")]
    public PropSet.Part[] parts;
    [Tooltip("The same parts' materials, tinted brown for trampled clumps.")]
    public Material[] trampledMaterials;
    [Tooltip("7 numbers per clump, in world space: x, y, z, yaw in degrees, scale x, y, z.")]
    public float[] poses;

    public int Count => poses == null ? 0 : poses.Length / PropSet.PoseSize;
    public int TrampledCount { get; private set; }

    bool[] trampled;
    Matrix4x4[] standingPose, flatPose;       // [clump]
    Matrix4x4[][] standingList, flatList;     // [part][n], what is drawn
    int standingCount, flatCount;
    Bounds bounds;
    bool dirty = true;

    void OnEnable()
    {
        dirty = true;
        RenderPipelineManager.beginCameraRendering += Draw;
    }

    void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

    void OnValidate() => dirty = true;

    void Build()
    {
        dirty = false;
        int n = Count;
        trampled = new bool[n];
        TrampledCount = 0;
        standingPose = new Matrix4x4[n];
        flatPose = new Matrix4x4[n];
        var oldState = Random.state;
        Random.InitState(name.GetHashCode() ^ n);
        for (int k = 0; k < n; k++)
        {
            int o = k * PropSet.PoseSize;
            var p = new Vector3(poses[o], poses[o + 1], poses[o + 2]);
            var s = new Vector3(poses[o + 4], poses[o + 5], poses[o + 6]);
            standingPose[k] = Matrix4x4.TRS(p, Quaternion.Euler(0f, poses[o + 3], 0f), s);
            // Pushed over in a random direction, almost flat on the mud.
            var lean = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(Random.Range(65f, 80f), 0f, 0f);
            flatPose[k] = Matrix4x4.TRS(p, lean, new Vector3(s.x, s.y * 0.7f, s.z));
            if (k == 0) bounds = new Bounds(p, Vector3.zero);
            else bounds.Encapsulate(p);
        }
        Random.state = oldState;
        bounds.Expand(2f);

        int partCount = parts == null ? 0 : parts.Length;
        standingList = new Matrix4x4[partCount][];
        flatList = new Matrix4x4[partCount][];
        for (int i = 0; i < partCount; i++)
        {
            standingList[i] = new Matrix4x4[n];
            flatList[i] = new Matrix4x4[n];
        }
        Refill();
    }

    // The draw lists: standing clumps and trampled clumps.
    void Refill()
    {
        standingCount = flatCount = 0;
        for (int k = 0; k < Count; k++)
        {
            for (int i = 0; i < standingList.Length; i++)
            {
                if (trampled[k]) flatList[i][flatCount] = flatPose[k] * parts[i].local;
                else standingList[i][standingCount] = standingPose[k] * parts[i].local;
            }
            if (trampled[k]) flatCount++;
            else standingCount++;
        }
    }

    // Flattens the standing clump nearest to the point (an elephant's mouth). False if none is left.
    public bool TrampleNearest(Vector3 at)
    {
        if (dirty) Build();
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int k = 0; k < Count; k++)
        {
            if (trampled[k]) continue;
            var c = standingPose[k].GetColumn(3);
            float d = (c.x - at.x) * (c.x - at.x) + (c.z - at.z) * (c.z - at.z);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = k;
            }
        }
        if (best < 0) return false;
        trampled[best] = true;
        TrampledCount++;
        Refill();
        return true;
    }

    void Draw(ScriptableRenderContext context, Camera cam)
    {
        if (cam == null || cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
        if (dirty) Build();
        if (parts == null) return;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].mesh == null) continue;
            if (standingCount > 0 && parts[i].material != null)
                Graphics.RenderMeshInstanced(Params(parts[i].material, cam), parts[i].mesh, parts[i].submesh, standingList[i], standingCount);
            var brown = trampledMaterials != null && i < trampledMaterials.Length ? trampledMaterials[i] : null;
            if (flatCount > 0 && brown != null)
                Graphics.RenderMeshInstanced(Params(brown, cam), parts[i].mesh, parts[i].submesh, flatList[i], flatCount);
        }
    }

    RenderParams Params(Material material, Camera cam) => new RenderParams(material)
    {
        shadowCastingMode = ShadowCastingMode.Off,
        receiveShadows = true,
        layer = gameObject.layer,
        worldBounds = bounds,
        camera = cam,
    };
}
