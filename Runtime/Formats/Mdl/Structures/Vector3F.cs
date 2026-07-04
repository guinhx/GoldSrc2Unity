using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Lightweight Vector3 for binary-compatible struct layouts.
    /// Does not depend on UnityEngine.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vector3F
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3F(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
