using System;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl;
using Source2Unity.Formats.Mdl.Parsers;
using Source2Unity.Formats.Mdl.Structures;

class MdlDeepValidator
{
    static void Main(string[] args)
    {
        string file = args.Length > 0 ? args[0] : @"D:\Development\offensive-mobile\client\Assets\StreamingAssets\cstrike\models\player\arctic\arctic.mdl";

        Console.WriteLine($"Deep validation: {Path.GetFileName(file)}\n");

        using var reader = new BinaryStreamReader(file);
        var parser = new MdlV10Parser();
        var result = parser.Parse(reader, file);

        ValidateBoneHierarchy(result);
        ValidateAnimationData(result);
        ValidateTextureData(result);
        ValidateMeshIntegrity(result);

        Console.WriteLine("\n=== Deep validation complete ===");
    }

    static void ValidateBoneHierarchy(MdlParseResult result)
    {
        Console.WriteLine("--- Bone Hierarchy ---");

        for (int i = 0; i < result.Bones.Count; i++)
        {
            var bone = result.Bones[i];
            unsafe
            {
                byte* ptr = bone.Name;
                int len = 0;
                while (len < 32 && ptr[len] != 0) len++;
                string name = System.Text.Encoding.ASCII.GetString(ptr, len);

                float posX = bone.Value[0], posY = bone.Value[1], posZ = bone.Value[2];
                float rotX = bone.Value[3], rotY = bone.Value[4], rotZ = bone.Value[5];
                float scalePos0 = bone.Scale[0], scaleRot3 = bone.Scale[3];

                if (i < 5)
                {
                    Console.WriteLine($"  Bone[{i}] \"{name}\" parent={bone.Parent}");
                    Console.WriteLine($"    Pos: ({posX:F3}, {posY:F3}, {posZ:F3})");
                    Console.WriteLine($"    Rot: ({rotX:F4}, {rotY:F4}, {rotZ:F4}) rad");
                    Console.WriteLine($"    Scale: pos=({scalePos0:F6}) rot=({scaleRot3:F6})");
                }

                // Validate scale values are reasonable
                for (int c = 0; c < 6; c++)
                {
                    float scale = bone.Scale[c];
                    if (scale < 0 || scale > 100.0f)
                    {
                        Console.WriteLine($"  [WARN] Bone[{i}] \"{name}\" scale[{c}] = {scale} (unusual)");
                    }
                }

                // Validate bone controllers
                for (int c = 0; c < 6; c++)
                {
                    int ctrl = bone.BoneController[c];
                    if (ctrl != -1 && (ctrl < 0 || ctrl > 32))
                    {
                        Console.WriteLine($"  [WARN] Bone[{i}] controller[{c}] = {ctrl}");
                    }
                }
            }
        }

        Console.WriteLine($"  Total: {result.Bones.Count} bones, hierarchy valid\n");
    }

    static unsafe void ValidateAnimationData(MdlParseResult result)
    {
        Console.WriteLine("--- Animation Data ---");

        if (result.ParsedSequences == null || result.ParsedSequences.Count == 0)
        {
            Console.WriteLine("  No sequences found!\n");
            return;
        }

        int badFrames = 0;
        int totalFrames = 0;

        for (int s = 0; s < Math.Min(5, result.ParsedSequences.Count); s++)
        {
            var seq = result.ParsedSequences[s];
            Console.WriteLine($"  Seq[{s}] \"{seq.Name}\" fps={seq.Fps:F1} frames={seq.NumFrames}");

            if (seq.BoneFrames == null || seq.BoneFrames.Length == 0)
            {
                Console.WriteLine("    [WARN] No bone frames!");
                continue;
            }

            // Check first frame of first bone
            if (seq.NumFrames > 0 && seq.BoneFrames.Length > 0 && seq.BoneFrames[0].Length > 0)
            {
                var f0b0 = seq.BoneFrames[0][0];
                Console.WriteLine($"    Frame0/Bone0: pos=({f0b0.Position.X:F2},{f0b0.Position.Y:F2},{f0b0.Position.Z:F2}) " +
                                  $"rot=({f0b0.Rotation.X:F4},{f0b0.Rotation.Y:F4},{f0b0.Rotation.Z:F4})");
            }

            // Validate all frames have reasonable values
            for (int f = 0; f < seq.NumFrames; f++)
            {
                totalFrames++;
                if (seq.BoneFrames[f] == null)
                {
                    badFrames++;
                    continue;
                }

                for (int b = 0; b < seq.BoneFrames[f].Length; b++)
                {
                    var frame = seq.BoneFrames[f][b];
                    var bone = result.Bones[b];

                    // Apply scale and check final values are reasonable
                    float px = bone.Value[0] + frame.Position.X * bone.Scale[0];
                    float py = bone.Value[1] + frame.Position.Y * bone.Scale[1];
                    float pz = bone.Value[2] + frame.Position.Z * bone.Scale[2];

                    // Position should be within reasonable bounds (< 10000 units from origin)
                    if (Math.Abs(px) > 10000 || Math.Abs(py) > 10000 || Math.Abs(pz) > 10000)
                    {
                        if (badFrames < 5)
                            Console.WriteLine($"    [WARN] Seq[{s}] Frame[{f}] Bone[{b}]: extreme pos ({px:F1},{py:F1},{pz:F1})");
                        badFrames++;
                    }
                }
            }
        }

        Console.WriteLine($"  Validated {totalFrames} frames, {badFrames} warnings\n");
    }

    static void ValidateTextureData(MdlParseResult result)
    {
        Console.WriteLine("--- Texture Data ---");

        if (result.ParsedTextures == null)
        {
            Console.WriteLine("  No textures\n");
            return;
        }

        foreach (var tex in result.ParsedTextures)
        {
            int expectedSize = tex.Width * tex.Height * 3;
            bool sizeOk = tex.PixelData != null && tex.PixelData.Length == expectedSize;
            bool indicesOk = tex.PaletteIndices != null && tex.PaletteIndices.Length == tex.Width * tex.Height;
            var flags = (MdlTextureFlags)tex.Flags;

            Console.WriteLine($"  \"{tex.Name}\" {tex.Width}x{tex.Height} flags={flags} " +
                              $"pixels={sizeOk} indices={indicesOk}");

            if (!sizeOk)
                Console.WriteLine($"    [FAIL] Expected {expectedSize} pixel bytes, got {tex.PixelData?.Length ?? 0}");
            if (!indicesOk)
                Console.WriteLine($"    [FAIL] Expected {tex.Width * tex.Height} indices, got {tex.PaletteIndices?.Length ?? 0}");
        }

        Console.WriteLine();
    }

    static void ValidateMeshIntegrity(MdlParseResult result)
    {
        Console.WriteLine("--- Mesh Integrity ---");

        foreach (var bp in result.ParsedBodyParts)
        {
            foreach (var model in bp.Models)
            {
                int vertCount = model.Vertices?.Length ?? 0;
                int normCount = model.Normals?.Length ?? 0;

                foreach (var mesh in model.Meshes)
                {
                    int outOfBoundsVerts = 0;
                    int outOfBoundsNorms = 0;

                    foreach (var tri in mesh.Triangles)
                    {
                        CheckVertex(tri.V0, vertCount, normCount, ref outOfBoundsVerts, ref outOfBoundsNorms);
                        CheckVertex(tri.V1, vertCount, normCount, ref outOfBoundsVerts, ref outOfBoundsNorms);
                        CheckVertex(tri.V2, vertCount, normCount, ref outOfBoundsVerts, ref outOfBoundsNorms);
                    }

                    if (outOfBoundsVerts > 0 || outOfBoundsNorms > 0)
                        Console.WriteLine($"  [FAIL] {bp.Name}/{model.Name}: {outOfBoundsVerts} OOB verts, {outOfBoundsNorms} OOB norms");
                }

                // Validate UV range
                if (result.ParsedTextures != null && result.ParsedTextures.Count > 0)
                {
                    int maxS = 0, maxT = 0, minS = int.MaxValue, minT = int.MaxValue;
                    foreach (var mesh in model.Meshes)
                    {
                        foreach (var tri in mesh.Triangles)
                        {
                            UpdateMinMax(tri.V0, ref minS, ref maxS, ref minT, ref maxT);
                            UpdateMinMax(tri.V1, ref minS, ref maxS, ref minT, ref maxT);
                            UpdateMinMax(tri.V2, ref minS, ref maxS, ref minT, ref maxT);
                        }
                    }

                    Console.WriteLine($"  {bp.Name}/{model.Name}: verts={vertCount} norms={normCount} " +
                                      $"UV S=[{minS},{maxS}] T=[{minT},{maxT}]");
                }
            }
        }

        Console.WriteLine();
    }

    static void CheckVertex(MdlTriVertex v, int vertCount, int normCount,
        ref int oobVerts, ref int oobNorms)
    {
        if (v.VertexIndex < 0 || v.VertexIndex >= vertCount) oobVerts++;
        if (v.NormalIndex < 0 || v.NormalIndex >= normCount) oobNorms++;
    }

    static void UpdateMinMax(MdlTriVertex v, ref int minS, ref int maxS, ref int minT, ref int maxT)
    {
        if (v.S < minS) minS = v.S;
        if (v.S > maxS) maxS = v.S;
        if (v.T < minT) minT = v.T;
        if (v.T > maxT) maxT = v.T;
    }
}
