using System;
using UnityEngine;

// The village's many small things (plants, trees, rocks, garden fences): which model parts to draw
// and where every copy stands. Made by the village builder (Editor/VillageBuilder.cs) and drawn by
// InstancedProps.
public class PropSet : ScriptableObject
{
    public const int PoseSize = 7; // x, y, z, yaw (degrees), scale x, y, z

    [Serializable]
    public class Part
    {
        public Mesh mesh;
        public int submesh;
        public Material material;      // a copy of the model's material with GPU instancing on
        public Matrix4x4 local;        // where this part sits inside the model
    }

    [Serializable]
    public class Group
    {
        public string name;
        public Part[] parts;
        public bool shadows;
        [Tooltip("Copies are drawn from this distance (m) ...")]
        public float near;
        [Tooltip("... up to this distance (m).")]
        public float far;
        [Tooltip("Rough size of one copy (m), so the drawing areas are big enough.")]
        public float size;
        [Tooltip("7 numbers per copy: x, y, z, yaw in degrees, scale x, y, z.")]
        public float[] poses;

        public int Count => poses == null ? 0 : poses.Length / PoseSize;
    }

    public Group[] groups;

    public int Copies
    {
        get
        {
            int n = 0;
            if (groups != null) foreach (var g in groups) n += g.Count;
            return n;
        }
    }
}
