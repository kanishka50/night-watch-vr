using UnityEngine;
using UnityEngine.Rendering;

// Draws the rice clumps of one crop plot (the village's rice_gold model, on the village's own
// clump spots) with GPU instancing: one draw for the standing clumps and one for the flattened
// ones. Each clump is standing, flat (stepped on: lies over, brown) or eaten (gone). CropPlot
// decides which: the trunk eats the clump nearest the mouth, the feet flatten what is under
// them. Made by ElephantSetup.
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
    public int StandingCount => standingCount;

    enum Clump : byte { Standing, Flat, Eaten }
    Clump[] state;
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
        state = new Clump[n];
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

    // The draw lists: standing clumps and flat clumps (eaten ones are not drawn).
    void Refill()
    {
        standingCount = flatCount = 0;
        for (int k = 0; k < Count; k++)
        {
            if (state[k] == Clump.Eaten) continue;
            bool flat = state[k] == Clump.Flat;
            for (int i = 0; i < standingList.Length; i++)
            {
                if (flat) flatList[i][flatCount] = flatPose[k] * parts[i].local;
                else standingList[i][standingCount] = standingPose[k] * parts[i].local;
            }
            if (flat) flatCount++;
            else standingCount++;
        }
    }

    static float FlatDistance(Matrix4x4 pose, Vector3 at)
    {
        float dx = pose.m03 - at.x, dz = pose.m23 - at.z;
        return dx * dx + dz * dz;
    }

    // The trunk takes the standing clump nearest the mouth: it is gone. False if none is left.
    public bool EatNearest(Vector3 mouth)
    {
        if (dirty) Build();
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int k = 0; k < Count; k++)
        {
            if (state[k] != Clump.Standing) continue;
            float d = FlatDistance(standingPose[k], mouth);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = k;
            }
        }
        if (best < 0) return false;
        state[best] = Clump.Eaten;
        Refill();
        return true;
    }

    // A foot flattens the standing clumps within this radius of it. Returns how many.
    public int FlattenAround(Vector3 foot, float radius)
    {
        if (dirty) Build();
        int flattened = 0;
        for (int k = 0; k < Count; k++)
        {
            if (state[k] != Clump.Standing || FlatDistance(standingPose[k], foot) > radius * radius) continue;
            state[k] = Clump.Flat;
            flattened++;
        }
        if (flattened > 0) Refill();
        return flattened;
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
