using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Draws the village's thousands of plants, trees and small props cheaply:
//  - every model part is drawn with GPU instancing, in one draw for all its near copies
//    (the CPU cost is the number of draws, so this matters most on the laptop);
//  - each camera keeps a list of the copies to draw: near enough for that model, not past the
//    point where the fog hides everything (about 55 m at night, 330 m by day), and in front of
//    the camera. The list is updated every 0.25 s, or sooner when the camera moves or turns;
//  - heavy trees switch to a cheap tree when far, and far rice and grass are thinned (both are
//    separate groups with near and far distances, made by the village builder).
// The world is cut into areas only to skip far or out-of-view areas quickly.
// The copies come from a PropSet made by the village builder. Works in edit mode too, so the
// village shows in the Scene view.
[ExecuteAlways]
public class InstancedProps : MonoBehaviour
{
    public PropSet set;
    [Tooltip("Multiplies every drawing distance. Lower = faster, fewer far plants.")]
    [Range(0.3f, 2f)] public float distanceScale = 1f;
    [Tooltip("Stop drawing where the fog hides everything.")]
    public bool cutAtFog = true;
    [Tooltip("Let the groups marked for shadows cast them. Costs a second drawing pass, and our shadow distance is short.")]
    public bool castShadows;
    [Tooltip("Size of one area (m), used to skip far or out-of-view areas quickly.")]
    public float cellSize = 48f;
    [Tooltip("How often (s) each camera's list of copies is updated.")]
    public float refresh = 0.25f;
    [Tooltip("Update the list sooner when the camera turns more than this (degrees).")]
    public float turnLimit = 8f;

    [Header("Last update, Game view (read only)")]
    public int copiesDrawn;
    public int drawCalls;

    const int MaxPerDraw = 1023;

    // The copies of one model that stand in one area (indices into the group).
    class Cell
    {
        public int group;
        public int[] copies;
        public Bounds bounds;
    }

    // What one camera draws of one group: the matrices of each part, the same copies for all.
    class GroupView
    {
        public Matrix4x4[][] visible;   // [part][n]
        public int count;
    }

    class CameraState
    {
        public float nextRefresh;
        public Vector3 lastPosition, lastForward;
        public GroupView[] groups;
    }

    readonly List<Cell> cells = new List<Cell>();
    readonly Dictionary<Camera, CameraState> cameras = new Dictionary<Camera, CameraState>();
    readonly Plane[] planes = new Plane[6];
    Vector3[][] points;        // [group][copy] copy positions
    Matrix4x4[][][] matrices;  // [group][part][copy] final matrices
    Bounds[] groupBounds;
    RenderParams[][] partParams;
    bool dirty = true;

    void OnEnable()
    {
        dirty = true;
        RenderPipelineManager.beginCameraRendering += Draw;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Draw;
        cells.Clear();
        cameras.Clear();
    }

    void OnValidate() => dirty = true;

    // Works out every part's final matrix once and sorts the copies into areas.
    void Build()
    {
        dirty = false;
        cells.Clear();
        cameras.Clear();
        if (set == null || set.groups == null) return;

        int groupCount = set.groups.Length;
        var toWorld = transform.localToWorldMatrix;
        points = new Vector3[groupCount][];
        matrices = new Matrix4x4[groupCount][][];
        groupBounds = new Bounds[groupCount];
        partParams = new RenderParams[groupCount][];

        for (int g = 0; g < groupCount; g++)
        {
            var group = set.groups[g];
            var parts = group.parts ?? new PropSet.Part[0];
            int n = parts.Length == 0 ? 0 : group.Count;
            bool shadows = castShadows && group.shadows;

            partParams[g] = new RenderParams[parts.Length];
            for (int p = 0; p < parts.Length; p++)
            {
                partParams[g][p] = new RenderParams(parts[p].material)
                {
                    shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    layer = gameObject.layer,
                };
            }

            points[g] = new Vector3[n];
            matrices[g] = new Matrix4x4[parts.Length][];
            for (int p = 0; p < parts.Length; p++) matrices[g][p] = new Matrix4x4[n];

            var byArea = new Dictionary<Vector2Int, List<int>>();
            for (int k = 0; k < n; k++)
            {
                int o = k * PropSet.PoseSize;
                var pose = toWorld * Matrix4x4.TRS(
                    new Vector3(group.poses[o], group.poses[o + 1], group.poses[o + 2]),
                    Quaternion.Euler(0f, group.poses[o + 3], 0f),
                    new Vector3(group.poses[o + 4], group.poses[o + 5], group.poses[o + 6]));
                var point = (Vector3)pose.GetColumn(3);
                points[g][k] = point;
                for (int p = 0; p < parts.Length; p++) matrices[g][p][k] = pose * parts[p].local;

                if (k == 0) groupBounds[g] = new Bounds(point, Vector3.zero);
                else groupBounds[g].Encapsulate(point);

                var key = new Vector2Int(Mathf.FloorToInt(point.x / cellSize), Mathf.FloorToInt(point.z / cellSize));
                if (!byArea.TryGetValue(key, out var list)) byArea[key] = list = new List<int>();
                list.Add(k);
            }
            groupBounds[g].Expand(group.size * 2f);

            foreach (var list in byArea.Values)
            {
                var cell = new Cell { group = g, copies = list.ToArray() };
                cell.bounds = new Bounds(points[g][list[0]], Vector3.zero);
                foreach (int k in list) cell.bounds.Encapsulate(points[g][k]);
                cell.bounds.Expand(group.size * 2f);
                cells.Add(cell);
            }
        }
    }

    // Called by the render pipeline just before each camera draws (Game view, Scene view, XR).
    void Draw(ScriptableRenderContext context, Camera cam)
    {
        if (cam == null || cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
        if (dirty) Build();
        if (set == null || cells.Count == 0) return;

        if (!cameras.TryGetValue(cam, out var state))
            cameras[cam] = state = new CameraState { groups = new GroupView[set.groups.Length], nextRefresh = -1f };

        var position = cam.transform.position;
        var forward = cam.transform.forward;
        float now = Time.realtimeSinceStartup;
        if (now >= state.nextRefresh || (position - state.lastPosition).sqrMagnitude > 4f ||
            Vector3.Angle(forward, state.lastForward) > turnLimit)
        {
            int near = Refresh(state, cam);
            if (cam.cameraType == CameraType.Game) copiesDrawn = near;
            state.nextRefresh = now + refresh;
            state.lastPosition = position;
            state.lastForward = forward;
        }

        int calls = 0;
        for (int g = 0; g < state.groups.Length; g++)
        {
            var view = state.groups[g];
            if (view == null || view.count == 0) continue;
            var parts = set.groups[g].parts;
            for (int p = 0; p < parts.Length; p++)
            {
                var rp = partParams[g][p];
                if (rp.material == null || parts[p].mesh == null) continue;
                rp.worldBounds = groupBounds[g];
                rp.camera = cam;
                for (int start = 0; start < view.count; start += MaxPerDraw)
                {
                    Graphics.RenderMeshInstanced(rp, parts[p].mesh, parts[p].submesh, view.visible[p],
                        Mathf.Min(MaxPerDraw, view.count - start), start);
                    calls++;
                }
            }
        }
        if (cam.cameraType == CameraType.Game) drawCalls = calls;
    }

    // Picks, for one camera, the copies to draw: near enough, before the fog, in front.
    int Refresh(CameraState state, Camera cam)
    {
        float fogFar = float.MaxValue;
        // Exponential squared fog: past 2 / density less than 2% of the colour is left.
        if (cutAtFog && RenderSettings.fog && RenderSettings.fogMode == FogMode.ExponentialSquared && RenderSettings.fogDensity > 0f)
            fogFar = 2f / RenderSettings.fogDensity;

        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        var eye = cam.transform.position;
        // The list is kept until the camera turns turnLimit degrees, so the view is widened by
        // that much (plus a little) to avoid plants popping in at the edges while turning.
        float widen = Mathf.Tan((turnLimit + 4f) * Mathf.Deg2Rad);

        for (int g = 0; g < state.groups.Length; g++)
            if (state.groups[g] != null) state.groups[g].count = 0;

        int total = 0;
        foreach (var cell in cells)
        {
            int g = cell.group;
            var group = set.groups[g];
            float near = group.near * distanceScale;
            float far = Mathf.Min(group.far * distanceScale, fogFar + group.size);
            if (near >= far) continue;

            // Skip the whole area if it is too far, or well outside the view.
            float edge = Mathf.Sqrt(cell.bounds.SqrDistance(eye));
            if (edge > far) continue;
            var wide = cell.bounds;
            wide.Expand(2f * widen * (edge + cell.bounds.extents.magnitude));
            if (!GeometryUtility.TestPlanesAABB(planes, wide)) continue;

            var view = state.groups[g];
            if (view == null)
            {
                int parts = matrices[g].Length;
                view = state.groups[g] = new GroupView { visible = new Matrix4x4[parts][] };
                for (int p = 0; p < parts; p++) view.visible[p] = new Matrix4x4[points[g].Length];
            }

            float near2 = near * near, far2 = far * far, radius = group.size;
            var pts = points[g];
            var mats = matrices[g];
            int n = view.count;
            foreach (int k in cell.copies)
            {
                var pt = pts[k];
                float dx = pt.x - eye.x, dz = pt.z - eye.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < near2 || d2 >= far2) continue;

                // In front of the camera, with a margin that grows with distance.
                float margin = radius + widen * Mathf.Sqrt(d2);
                bool inside = true;
                for (int i = 0; i < 6; i++)
                {
                    if (planes[i].GetDistanceToPoint(pt) < -margin) { inside = false; break; }
                }
                if (!inside) continue;

                for (int p = 0; p < mats.Length; p++) view.visible[p][n] = mats[p][k];
                n++;
            }
            total += n - view.count;
            view.count = n;
        }
        return total;
    }
}
