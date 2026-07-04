using System.Collections.Generic;
using Source2Unity.Formats.Mdl.Structures;

namespace Source2Unity.Formats.Mdl.Parsers
{
    public interface IMdlParser
    {
        MdlParseResult Parse(string filePath);
    }

    public sealed class MdlParseResult
    {
        public MdlHeader Header { get; init; }
        public MdlVersion Version { get; init; }
        public string ModelName { get; init; }
        public IReadOnlyList<MdlBone> Bones { get; init; }
        public IReadOnlyList<MdlBoneController> BoneControllers { get; init; }
        public IReadOnlyList<MdlHitBox> HitBoxes { get; init; }
        public IReadOnlyList<MdlSequenceDesc> Sequences { get; init; }
        public IReadOnlyList<MdlSequenceGroup> SequenceGroups { get; init; }
        public IReadOnlyList<MdlTexture> Textures { get; init; }
        public IReadOnlyList<MdlBodyPart> BodyParts { get; init; }
        public IReadOnlyList<MdlAttachment> Attachments { get; init; }
        public IReadOnlyList<MdlParsedBodyPart> ParsedBodyParts { get; init; }
        public IReadOnlyList<MdlParsedTexture> ParsedTextures { get; init; }
        public IReadOnlyList<MdlParsedSequence> ParsedSequences { get; init; }
    }

    public sealed class MdlParsedBodyPart
    {
        public string Name { get; init; }
        public IReadOnlyList<MdlParsedModel> Models { get; init; }
    }

    public sealed class MdlParsedModel
    {
        public string Name { get; init; }
        public MdlModel ModelStruct { get; init; }
        public Vector3F[] Vertices { get; init; }
        public Vector3F[] Normals { get; init; }
        public byte[] VertexBoneIndices { get; init; }
        public byte[] NormalBoneIndices { get; init; }
        public IReadOnlyList<MdlParsedMesh> Meshes { get; init; }
    }

    public sealed class MdlParsedMesh
    {
        public MdlMesh MeshStruct { get; init; }
        public IReadOnlyList<MdlTriangle> Triangles { get; init; }
    }

    public struct MdlTriangle
    {
        public MdlTriVertex V0;
        public MdlTriVertex V1;
        public MdlTriVertex V2;
    }

    public struct MdlTriVertex
    {
        public short VertexIndex;
        public short NormalIndex;
        public short S;
        public short T;
    }

    public sealed class MdlParsedTexture
    {
        public string Name { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int Flags { get; init; }
        public byte[] PixelData { get; init; }
    }

    public sealed class MdlParsedSequence
    {
        public string Name { get; init; }
        public MdlSequenceDesc Descriptor { get; init; }
        public float Fps { get; init; }
        public int NumFrames { get; init; }
        public MdlBoneFrame[][] BoneFrames { get; init; }
    }

    public struct MdlBoneFrame
    {
        public Vector3F Position;
        public Vector3F Rotation;
    }
}
