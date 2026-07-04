using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl.Structures;

namespace Source2Unity.Formats.Mdl.Parsers
{
    public sealed class MdlV10Parser : IMdlParser
    {
        public MdlParseResult Parse(string filePath)
        {
            using var reader = new BinaryStreamReader(filePath);
            return Parse(reader, filePath);
        }

        public MdlParseResult Parse(BinaryStreamReader reader, string filePath)
        {
            var header = reader.ReadStruct<MdlHeader>();
            ValidateHeader(header);

            string modelName = ReadFixedName(header);

            var bones = ReadArray<MdlBone>(reader, header.BoneIndex, header.NumBones);
            var boneControllers = ReadArray<MdlBoneController>(reader, header.BoneControllerIndex, header.NumBoneControllers);
            var hitBoxes = ReadArray<MdlHitBox>(reader, header.HitBoxIndex, header.NumHitBoxes);
            var sequences = ReadArray<MdlSequenceDesc>(reader, header.SeqIndex, header.NumSeq);
            var sequenceGroups = ReadArray<MdlSequenceGroup>(reader, header.SeqGroupIndex, header.NumSeqGroups);
            var textures = ReadArray<MdlTexture>(reader, header.TextureIndex, header.NumTextures);
            var bodyParts = ReadArray<MdlBodyPart>(reader, header.BodyPartIndex, header.NumBodyParts);
            var attachments = ReadArray<MdlAttachment>(reader, header.AttachmentIndex, header.NumAttachments);

            var parsedTextures = ParseTextures(reader, textures, filePath, header);
            var parsedBodyParts = ParseBodyParts(reader, bodyParts);
            var parsedSequences = ParseSequences(reader, sequences, sequenceGroups, bones.Length, filePath);

            return new MdlParseResult
            {
                Header = header,
                Version = MdlVersion.GoldSrc,
                ModelName = modelName,
                Bones = bones,
                BoneControllers = boneControllers,
                HitBoxes = hitBoxes,
                Sequences = sequences,
                SequenceGroups = sequenceGroups,
                Textures = textures,
                BodyParts = bodyParts,
                Attachments = attachments,
                ParsedBodyParts = parsedBodyParts,
                ParsedTextures = parsedTextures,
                ParsedSequences = parsedSequences
            };
        }

        private static void ValidateHeader(in MdlHeader header)
        {
            if (header.Id != MdlConstants.MagicIdst)
                throw new InvalidDataException($"Invalid MDL magic: 0x{header.Id:X8}, expected IDST (0x{MdlConstants.MagicIdst:X8}).");
            if (header.Version != MdlConstants.VersionGoldSrc)
                throw new InvalidDataException($"Unsupported MDL version: {header.Version}, expected {MdlConstants.VersionGoldSrc}.");
        }

        private static unsafe string ReadFixedName(in MdlHeader header)
        {
            fixed (byte* ptr = header.Name)
            {
                int len = 0;
                while (len < 64 && ptr[len] != 0) len++;
                return Encoding.ASCII.GetString(ptr, len);
            }
        }

        private static unsafe T[] ReadArray<T>(BinaryStreamReader reader, int offset, int count) where T : unmanaged
        {
            if (count <= 0) return Array.Empty<T>();
            reader.Seek(offset);
            return reader.ReadStructArray<T>(count);
        }

        #region Texture Parsing

        private static List<MdlParsedTexture> ParseTextures(
            BinaryStreamReader reader,
            MdlTexture[] textures,
            string filePath,
            in MdlHeader header)
        {
            var result = new List<MdlParsedTexture>(textures.Length);

            BinaryStreamReader textureReader = reader;
            bool externalTextures = textures.Length == 0 && header.NumTextures == 0;

            if (externalTextures)
            {
                string texturePath = GetExternalTexturePath(filePath);
                if (!File.Exists(texturePath))
                    return result;

                textureReader = new BinaryStreamReader(texturePath);
                var texHeader = textureReader.ReadStruct<MdlHeader>();
                textures = ReadArray<MdlTexture>(textureReader, texHeader.TextureIndex, texHeader.NumTextures);
            }

            try
            {
                foreach (var tex in textures)
                {
                    result.Add(ParseSingleTexture(textureReader, tex));
                }
            }
            finally
            {
                if (externalTextures)
                    textureReader.Dispose();
            }

            return result;
        }

        private static unsafe MdlParsedTexture ParseSingleTexture(BinaryStreamReader reader, in MdlTexture texture)
        {
            string name;
            fixed (byte* ptr = texture.Name)
            {
                int len = 0;
                while (len < 64 && ptr[len] != 0) len++;
                name = Encoding.ASCII.GetString(ptr, len);
            }

            int pixelCount = texture.Width * texture.Height;
            reader.Seek(texture.Index);

            var indices = reader.ReadBytes(pixelCount);
            var paletteBytes = reader.ReadBytes(256 * 3);

            var pixelData = new byte[pixelCount * 3];
            for (int i = 0; i < pixelCount; i++)
            {
                int paletteIndex = indices[i] * 3;
                pixelData[i * 3 + 0] = paletteBytes[paletteIndex + 0];
                pixelData[i * 3 + 1] = paletteBytes[paletteIndex + 1];
                pixelData[i * 3 + 2] = paletteBytes[paletteIndex + 2];
            }

            return new MdlParsedTexture
            {
                Name = name,
                Width = texture.Width,
                Height = texture.Height,
                Flags = texture.Flags,
                PixelData = pixelData,
                PaletteIndices = indices
            };
        }

        #endregion

        #region Body Part / Model / Mesh Parsing

        private static List<MdlParsedBodyPart> ParseBodyParts(BinaryStreamReader reader, MdlBodyPart[] bodyParts)
        {
            var result = new List<MdlParsedBodyPart>(bodyParts.Length);

            foreach (var bp in bodyParts)
            {
                string bpName;
                unsafe
                {
                    fixed (byte* ptr = bp.Name)
                    {
                        int len = 0;
                        while (len < 64 && ptr[len] != 0) len++;
                        bpName = Encoding.ASCII.GetString(ptr, len);
                    }
                }

                reader.Seek(bp.ModelIndex);
                var models = reader.ReadStructArray<MdlModel>(bp.NumModels);
                var parsedModels = new List<MdlParsedModel>(bp.NumModels);

                foreach (var model in models)
                {
                    parsedModels.Add(ParseModel(reader, model));
                }

                result.Add(new MdlParsedBodyPart
                {
                    Name = bpName,
                    Models = parsedModels
                });
            }

            return result;
        }

        private static unsafe MdlParsedModel ParseModel(BinaryStreamReader reader, in MdlModel model)
        {
            string name;
            fixed (byte* ptr = model.Name)
            {
                int len = 0;
                while (len < 64 && ptr[len] != 0) len++;
                name = Encoding.ASCII.GetString(ptr, len);
            }

            reader.Seek(model.VertIndex);
            var vertices = reader.ReadStructArray<Vector3F>(model.NumVerts);

            reader.Seek(model.NormIndex);
            var normals = reader.ReadStructArray<Vector3F>(model.NumNorms);

            reader.Seek(model.VertInfoIndex);
            var vertBoneIndices = reader.ReadBytes(model.NumVerts);

            reader.Seek(model.NormInfoIndex);
            var normBoneIndices = reader.ReadBytes(model.NumNorms);

            reader.Seek(model.MeshIndex);
            var meshStructs = reader.ReadStructArray<MdlMesh>(model.NumMesh);
            var parsedMeshes = new List<MdlParsedMesh>(model.NumMesh);

            foreach (var mesh in meshStructs)
            {
                parsedMeshes.Add(ParseMesh(reader, mesh));
            }

            return new MdlParsedModel
            {
                Name = name,
                ModelStruct = model,
                Vertices = vertices,
                Normals = normals,
                VertexBoneIndices = vertBoneIndices,
                NormalBoneIndices = normBoneIndices,
                Meshes = parsedMeshes
            };
        }

        private static MdlParsedMesh ParseMesh(BinaryStreamReader reader, in MdlMesh mesh)
        {
            reader.Seek(mesh.TriIndex);
            var triangles = DecodeTriangleCommands(reader);

            return new MdlParsedMesh
            {
                MeshStruct = mesh,
                Triangles = triangles
            };
        }

        private static List<MdlTriangle> DecodeTriangleCommands(BinaryStreamReader reader)
        {
            var triangles = new List<MdlTriangle>();

            while (true)
            {
                short cmd = reader.ReadInt16();
                if (cmd == 0) break;

                bool isStrip = cmd > 0;
                int vertCount = Math.Abs(cmd);

                var verts = new MdlTriVertex[vertCount];
                for (int i = 0; i < vertCount; i++)
                {
                    verts[i] = new MdlTriVertex
                    {
                        VertexIndex = reader.ReadInt16(),
                        NormalIndex = reader.ReadInt16(),
                        S = reader.ReadInt16(),
                        T = reader.ReadInt16()
                    };
                }

                if (isStrip)
                {
                    for (int i = 2; i < vertCount; i++)
                    {
                        MdlTriangle tri;
                        if ((i & 1) == 0)
                        {
                            tri.V0 = verts[i - 2];
                            tri.V1 = verts[i - 1];
                            tri.V2 = verts[i];
                        }
                        else
                        {
                            tri.V0 = verts[i];
                            tri.V1 = verts[i - 1];
                            tri.V2 = verts[i - 2];
                        }
                        triangles.Add(tri);
                    }
                }
                else
                {
                    for (int i = 2; i < vertCount; i++)
                    {
                        MdlTriangle tri;
                        tri.V0 = verts[0];
                        tri.V1 = verts[i - 1];
                        tri.V2 = verts[i];
                        triangles.Add(tri);
                    }
                }
            }

            return triangles;
        }

        #endregion

        #region Sequence / Animation Parsing

        private static List<MdlParsedSequence> ParseSequences(
            BinaryStreamReader reader,
            MdlSequenceDesc[] sequences,
            MdlSequenceGroup[] seqGroups,
            int numBones,
            string filePath)
        {
            var result = new List<MdlParsedSequence>(sequences.Length);
            var externalReaders = new Dictionary<int, BinaryStreamReader>();

            try
            {
                foreach (var seq in sequences)
                {
                    string name;
                    unsafe
                    {
                        fixed (byte* ptr = seq.Label)
                        {
                            int len = 0;
                            while (len < 32 && ptr[len] != 0) len++;
                            name = Encoding.ASCII.GetString(ptr, len);
                        }
                    }

                    BinaryStreamReader animReader = reader;

                    if (seq.SeqGroup > 0)
                    {
                        if (!externalReaders.TryGetValue(seq.SeqGroup, out animReader))
                        {
                            string seqPath = GetExternalSequencePath(filePath, seq.SeqGroup);
                            if (!File.Exists(seqPath))
                            {
                                result.Add(new MdlParsedSequence
                                {
                                    Name = name,
                                    Descriptor = seq,
                                    Fps = seq.Fps,
                                    NumFrames = seq.NumFrames,
                                    BoneFrames = Array.Empty<MdlBoneFrame[]>()
                                });
                                continue;
                            }
                            animReader = new BinaryStreamReader(seqPath);
                            externalReaders[seq.SeqGroup] = animReader;
                        }
                    }

                    var boneFrames = DecompressAnimation(animReader, seq, numBones);

                    result.Add(new MdlParsedSequence
                    {
                        Name = name,
                        Descriptor = seq,
                        Fps = seq.Fps,
                        NumFrames = seq.NumFrames,
                        BoneFrames = boneFrames
                    });
                }
            }
            finally
            {
                foreach (var ext in externalReaders.Values)
                    ext.Dispose();
            }

            return result;
        }

        private static unsafe MdlBoneFrame[][] DecompressAnimation(
            BinaryStreamReader reader,
            in MdlSequenceDesc seq,
            int numBones)
        {
            var frames = new MdlBoneFrame[seq.NumFrames][];
            for (int f = 0; f < seq.NumFrames; f++)
                frames[f] = new MdlBoneFrame[numBones];

            long animBaseOffset = seq.AnimIndex;

            for (int bone = 0; bone < numBones; bone++)
            {
                long animStructOffset = animBaseOffset + bone * sizeof(MdlAnim);
                reader.Seek(animStructOffset);
                var anim = reader.ReadStruct<MdlAnim>();

                var posValues = new float[seq.NumFrames * 3];
                var rotValues = new float[seq.NumFrames * 3];

                for (int channel = 0; channel < 6; channel++)
                {
                    ushort offset = anim.Offset[channel];
                    if (offset == 0) continue;

                    long dataOffset = animStructOffset + offset;
                    reader.Seek(dataOffset);

                    for (int frame = 0; frame < seq.NumFrames;)
                    {
                        var control = reader.ReadStruct<MdlAnimValue>();
                        int valid = control.Valid;
                        int total = control.Total;

                        short lastValue = 0;
                        for (int j = 0; j < total && frame < seq.NumFrames; j++, frame++)
                        {
                            if (j < valid)
                            {
                                var val = reader.ReadStruct<MdlAnimValue>();
                                lastValue = val.Value;
                            }

                            if (channel < 3)
                                posValues[frame * 3 + channel] = lastValue;
                            else
                                rotValues[frame * 3 + (channel - 3)] = lastValue;
                        }
                    }
                }

                for (int f = 0; f < seq.NumFrames; f++)
                {
                    frames[f][bone] = new MdlBoneFrame
                    {
                        Position = new Vector3F(posValues[f * 3], posValues[f * 3 + 1], posValues[f * 3 + 2]),
                        Rotation = new Vector3F(rotValues[f * 3], rotValues[f * 3 + 1], rotValues[f * 3 + 2])
                    };
                }
            }

            return frames;
        }

        #endregion

        #region External Files

        internal static string GetExternalTexturePath(string mainFilePath)
        {
            string dir = Path.GetDirectoryName(mainFilePath) ?? ".";
            string baseName = Path.GetFileNameWithoutExtension(mainFilePath);
            string ext = Path.GetExtension(mainFilePath);
            return Path.Combine(dir, baseName + "T" + ext);
        }

        internal static string GetExternalSequencePath(string mainFilePath, int groupIndex)
        {
            string dir = Path.GetDirectoryName(mainFilePath) ?? ".";
            string baseName = Path.GetFileNameWithoutExtension(mainFilePath);
            string ext = Path.GetExtension(mainFilePath);
            return Path.Combine(dir, baseName + groupIndex.ToString("D2") + ext);
        }

        #endregion
    }
}
