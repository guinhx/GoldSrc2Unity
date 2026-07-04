using System;
using System.IO;
using System.Runtime.InteropServices;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl;
using Source2Unity.Formats.Mdl.Parsers;
using Source2Unity.Formats.Mdl.Structures;

class MdlValidator
{
    static int _errors = 0;
    static int _warnings = 0;
    static int _passed = 0;

    static void Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : @"D:\Development\offensive-mobile\client\Assets\StreamingAssets\cstrike\models";

        var files = Directory.GetFiles(dir, "*.mdl", SearchOption.AllDirectories);
        Console.WriteLine($"Found {files.Length} MDL files\n");

        // Test struct sizes first
        ValidateStructSizes();

        // Test a few representative files in detail
        string[] testFiles = {
            Path.Combine(dir, "player", "arctic", "arctic.mdl"),
            Path.Combine(dir, "v_ak47.mdl"),
            Path.Combine(dir, "v_knife.mdl"),
            Path.Combine(dir, "p_c4.mdl"),
        };

        foreach (var f in testFiles)
        {
            if (File.Exists(f))
                ValidateFile(f, verbose: true);
        }

        Console.WriteLine("\n--- Batch validation (all files) ---\n");

        foreach (var f in files)
            ValidateFile(f, verbose: false);

        Console.WriteLine($"\n=== RESULTS ===");
        Console.WriteLine($"  Passed: {_passed}");
        Console.WriteLine($"  Warnings: {_warnings}");
        Console.WriteLine($"  Errors: {_errors}");
    }

    static unsafe void ValidateStructSizes()
    {
        Console.WriteLine("=== Struct Size Validation ===");

        Check("MdlHeader", sizeof(MdlHeader), 244);
        Check("MdlBone", sizeof(MdlBone), 112);
        Check("MdlBoneController", sizeof(MdlBoneController), 24);
        Check("MdlHitBox", sizeof(MdlHitBox), 32);
        Check("MdlSequenceDesc", sizeof(MdlSequenceDesc), 176);
        Check("MdlSequenceGroup", sizeof(MdlSequenceGroup), 104);
        Check("MdlTexture", sizeof(MdlTexture), 80);
        Check("MdlBodyPart", sizeof(MdlBodyPart), 76);
        Check("MdlModel", sizeof(MdlModel), 112);
        Check("MdlMesh", sizeof(MdlMesh), 20);
        Check("MdlAttachment", sizeof(MdlAttachment), 88);
        Check("MdlAnim", sizeof(MdlAnim), 12);
        Check("MdlAnimValue", sizeof(MdlAnimValue), 2);
        Check("Vector3F", sizeof(Vector3F), 12);

        Console.WriteLine();
    }

    static void Check(string name, int actual, int expected)
    {
        if (actual == expected)
        {
            Console.WriteLine($"  [OK] {name}: {actual} bytes");
            _passed++;
        }
        else
        {
            Console.WriteLine($"  [FAIL] {name}: got {actual}, expected {expected}");
            _errors++;
        }
    }

    static void ValidateFile(string path, bool verbose)
    {
        string shortName = Path.GetFileName(path);
        try
        {
            using var reader = new BinaryStreamReader(path);

            // Read raw header
            var header = reader.ReadStruct<MdlHeader>();

            // Validate magic
            if (header.Id != MdlConstants.MagicIdst)
            {
                Console.WriteLine($"  [FAIL] {shortName}: Invalid magic 0x{header.Id:X8}");
                _errors++;
                return;
            }

            if (header.Version != MdlConstants.VersionGoldSrc)
            {
                Console.WriteLine($"  [WARN] {shortName}: Version {header.Version} (not v10)");
                _warnings++;
                return;
            }

            // Validate header offsets are within file bounds
            long fileLen = reader.Length;
            bool valid = true;

            valid &= ValidateOffset(shortName, "BoneIndex", header.BoneIndex, fileLen, verbose);
            valid &= ValidateOffset(shortName, "BoneControllerIndex", header.BoneControllerIndex, fileLen, verbose);
            valid &= ValidateOffset(shortName, "HitBoxIndex", header.HitBoxIndex, fileLen, verbose);
            valid &= ValidateOffset(shortName, "SeqIndex", header.SeqIndex, fileLen, verbose);
            valid &= ValidateOffset(shortName, "SeqGroupIndex", header.SeqGroupIndex, fileLen, verbose);
            valid &= ValidateOffset(shortName, "BodyPartIndex", header.BodyPartIndex, fileLen, verbose);

            if (header.NumTextures > 0)
                valid &= ValidateOffset(shortName, "TextureIndex", header.TextureIndex, fileLen, verbose);

            if (!valid)
            {
                _errors++;
                return;
            }

            // Try full parse
            reader.Position = 0;
            var parser = new MdlV10Parser();
            var result = parser.Parse(reader, path);

            // Validate parse result sanity
            if (result.Bones == null || result.Bones.Count == 0)
            {
                Console.WriteLine($"  [FAIL] {shortName}: No bones parsed");
                _errors++;
                return;
            }

            if (result.ParsedBodyParts == null || result.ParsedBodyParts.Count == 0)
            {
                Console.WriteLine($"  [FAIL] {shortName}: No body parts parsed");
                _errors++;
                return;
            }

            int totalVerts = 0;
            int totalTris = 0;
            foreach (var bp in result.ParsedBodyParts)
            {
                foreach (var model in bp.Models)
                {
                    totalVerts += model.Vertices?.Length ?? 0;
                    foreach (var mesh in model.Meshes)
                        totalTris += mesh.Triangles?.Count ?? 0;
                }
            }

            if (totalVerts == 0 || totalTris == 0)
            {
                Console.WriteLine($"  [FAIL] {shortName}: No geometry (verts={totalVerts}, tris={totalTris})");
                _errors++;
                return;
            }

            // Validate bone parent chain
            for (int i = 0; i < result.Bones.Count; i++)
            {
                int parent = result.Bones[i].Parent;
                if (parent >= i && parent != -1)
                {
                    Console.WriteLine($"  [FAIL] {shortName}: Bone {i} has forward parent ref {parent}");
                    _errors++;
                    return;
                }
            }

            // Validate vertex bone indices
            foreach (var bp in result.ParsedBodyParts)
            {
                foreach (var model in bp.Models)
                {
                    if (model.VertexBoneIndices != null)
                    {
                        foreach (byte bIdx in model.VertexBoneIndices)
                        {
                            if (bIdx >= result.Bones.Count)
                            {
                                Console.WriteLine($"  [FAIL] {shortName}: Vertex bone index {bIdx} >= bone count {result.Bones.Count}");
                                _errors++;
                                return;
                            }
                        }
                    }
                }
            }

            // Validate textures
            int texCount = result.ParsedTextures?.Count ?? 0;

            // Validate animations
            int seqCount = result.ParsedSequences?.Count ?? 0;
            int animFrames = 0;
            if (result.ParsedSequences != null)
            {
                foreach (var seq in result.ParsedSequences)
                    animFrames += seq.NumFrames;
            }

            if (verbose)
            {
                unsafe
                {
                    string modelName;
                    var h = header;
                    byte* ptr = h.Name;
                    int len = 0;
                    while (len < 64 && ptr[len] != 0) len++;
                    modelName = System.Text.Encoding.ASCII.GetString(ptr, len);

                    Console.WriteLine($"  [OK] {shortName}:");
                    Console.WriteLine($"       Name: {modelName}");
                    Console.WriteLine($"       Bones: {result.Bones.Count}");
                    Console.WriteLine($"       BodyParts: {result.ParsedBodyParts.Count}");
                    Console.WriteLine($"       Vertices: {totalVerts}, Triangles: {totalTris}");
                    Console.WriteLine($"       Textures: {texCount}");
                    Console.WriteLine($"       Sequences: {seqCount}, Total frames: {animFrames}");
                    Console.WriteLine($"       File size: {fileLen:N0} bytes");
                }
            }

            _passed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] {shortName}: {ex.GetType().Name}: {ex.Message}");
            if (verbose)
                Console.WriteLine($"         {ex.StackTrace?.Split('\n')[0]}");
            _errors++;
        }
    }

    static bool ValidateOffset(string file, string field, int offset, long fileLen, bool verbose)
    {
        if (offset < 0 || offset > fileLen)
        {
            if (verbose)
                Console.WriteLine($"  [FAIL] {file}: {field} offset {offset} exceeds file length {fileLen}");
            return false;
        }
        return true;
    }
}
