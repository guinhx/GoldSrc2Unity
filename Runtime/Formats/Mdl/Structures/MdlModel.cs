using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Studio model within a body part (mstudiomodel_t).
    /// Size: 112 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlModel
    {
        public fixed byte Name[64];

        public int Type;
        public float BoundingRadius;

        public int NumMesh;
        public int MeshIndex;

        public int NumVerts;
        public int VertInfoIndex;
        public int VertIndex;

        public int NumNorms;
        public int NormInfoIndex;
        public int NormIndex;

        public int NumGroups;
        public int GroupIndex;
    }
}
