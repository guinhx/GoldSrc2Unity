using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Mesh within a model (mstudiomesh_t).
    /// Size: 20 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MdlMesh
    {
        public int NumTris;
        public int TriIndex;
        public int SkinRef;
        public int NumNorms;
        public int NormIndex;
    }
}
