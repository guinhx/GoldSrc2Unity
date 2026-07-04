using System;
using System.Collections.Generic;
using Source2Unity.Formats.Mdl;
using Source2Unity.Formats.Mdl.Parsers;
using Source2Unity.Formats.Mdl.Structures;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Source2Unity.Editor.Importers
{
    [ScriptedImporter(3, "mdl")]
    public class MdlAssetImporter : ScriptedImporter
    {
        private const float GoldSrcScale = 0.0254f;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                var mdlFile = new MdlFile();
                var result = mdlFile.Read(ctx.assetPath);

                var root = new GameObject(result.ModelName ?? "MDL Model");
                ctx.AddObjectToAsset("root", root);
                ctx.SetMainObject(root);

                var boneAbsoluteMatrices = ComputeBoneMatricesGoldSrc(result);
                var bones = BuildBoneHierarchy(root, result);
                var textures = ImportTextures(ctx, result.ParsedTextures);
                var materials = CreateMaterials(ctx, textures, result.ParsedTextures);
                ImportMeshes(ctx, root, result, bones, materials, boneAbsoluteMatrices);
                ImportAnimations(ctx, root, result, bones);
            }
            catch (NotSupportedException ex)
            {
                ctx.LogImportWarning($"MDL format not supported: {ex.Message}");
            }
            catch (Exception ex)
            {
                ctx.LogImportError($"Failed to import MDL: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #region Bone Matrices (GoldSrc Space)

        private struct GoldSrcMatrix
        {
            public float M00, M01, M02, M03;
            public float M10, M11, M12, M13;
            public float M20, M21, M22, M23;

            public static GoldSrcMatrix FromAngleAndPos(float pitch, float yaw, float roll, float px, float py, float pz)
            {
                float sp = Mathf.Sin(pitch), cp = Mathf.Cos(pitch);
                float sy = Mathf.Sin(yaw),   cy = Mathf.Cos(yaw);
                float sr = Mathf.Sin(roll),  cr = Mathf.Cos(roll);

                return new GoldSrcMatrix
                {
                    M00 = cp * cy, M01 = sr * sp * cy + cr * (-sy), M02 = cr * sp * cy + (-sr) * (-sy), M03 = px,
                    M10 = cp * sy, M11 = sr * sp * sy + cr * cy,    M12 = cr * sp * sy + (-sr) * cy,    M13 = py,
                    M20 = -sp,     M21 = sr * cp,                   M22 = cr * cp,                      M23 = pz
                };
            }

            public static GoldSrcMatrix Concat(in GoldSrcMatrix a, in GoldSrcMatrix b)
            {
                GoldSrcMatrix o;
                o.M00 = a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20;
                o.M01 = a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21;
                o.M02 = a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22;
                o.M03 = a.M00 * b.M03 + a.M01 * b.M13 + a.M02 * b.M23 + a.M03;

                o.M10 = a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20;
                o.M11 = a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21;
                o.M12 = a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22;
                o.M13 = a.M10 * b.M03 + a.M11 * b.M13 + a.M12 * b.M23 + a.M13;

                o.M20 = a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20;
                o.M21 = a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21;
                o.M22 = a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22;
                o.M23 = a.M20 * b.M03 + a.M21 * b.M13 + a.M22 * b.M23 + a.M23;
                return o;
            }

            public Vector3 TransformPoint(float x, float y, float z)
            {
                return new Vector3(
                    M00 * x + M01 * y + M02 * z + M03,
                    M10 * x + M11 * y + M12 * z + M13,
                    M20 * x + M21 * y + M22 * z + M23);
            }

            public Vector3 TransformDirection(float x, float y, float z)
            {
                return new Vector3(
                    M00 * x + M01 * y + M02 * z,
                    M10 * x + M11 * y + M12 * z,
                    M20 * x + M21 * y + M22 * z);
            }
        }

        private static unsafe GoldSrcMatrix[] ComputeBoneMatricesGoldSrc(MdlParseResult result)
        {
            if (result.Bones == null || result.Bones.Count == 0)
                return Array.Empty<GoldSrcMatrix>();

            var matrices = new GoldSrcMatrix[result.Bones.Count];
            for (int i = 0; i < result.Bones.Count; i++)
            {
                var bone = result.Bones[i];
                var local = GoldSrcMatrix.FromAngleAndPos(
                    bone.Value[3], bone.Value[4], bone.Value[5],
                    bone.Value[0], bone.Value[1], bone.Value[2]);

                if (bone.Parent >= 0 && bone.Parent < i)
                    matrices[i] = GoldSrcMatrix.Concat(matrices[bone.Parent], local);
                else
                    matrices[i] = local;
            }
            return matrices;
        }

        #endregion

        #region Bone Hierarchy (Unity Transforms)

        private static Transform[] BuildBoneHierarchy(GameObject root, MdlParseResult result)
        {
            if (result.Bones == null || result.Bones.Count == 0)
                return Array.Empty<Transform>();

            var boneTransforms = new Transform[result.Bones.Count];
            var armature = new GameObject("Armature");
            armature.transform.SetParent(root.transform, false);

            for (int i = 0; i < result.Bones.Count; i++)
            {
                var bone = result.Bones[i];
                string boneName = GetBoneName(bone, i);

                var boneObj = new GameObject(boneName);
                boneTransforms[i] = boneObj.transform;

                if (bone.Parent >= 0 && bone.Parent < i)
                    boneObj.transform.SetParent(boneTransforms[bone.Parent], false);
                else
                    boneObj.transform.SetParent(armature.transform, false);

                unsafe
                {
                    boneObj.transform.localPosition = GoldSrcPositionToUnity(
                        bone.Value[0], bone.Value[1], bone.Value[2]);
                    boneObj.transform.localRotation = GoldSrcRotationToUnity(
                        bone.Value[3], bone.Value[4], bone.Value[5]);
                }
            }

            return boneTransforms;
        }

        private static unsafe string GetBoneName(MdlBone bone, int index)
        {
            byte* ptr = bone.Name;
            int len = 0;
            while (len < 32 && ptr[len] != 0) len++;
            if (len == 0) return $"bone_{index}";
            return System.Text.Encoding.ASCII.GetString(ptr, len);
        }

        #endregion

        #region Textures and Materials

        private static List<Texture2D> ImportTextures(AssetImportContext ctx, IReadOnlyList<MdlParsedTexture> textures)
        {
            var result = new List<Texture2D>();
            if (textures == null) return result;

            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                var flags = (MdlTextureFlags)tex.Flags;
                bool hasAlpha = (flags & MdlTextureFlags.Alpha) != 0 ||
                                (flags & MdlTextureFlags.Masked) != 0;

                var format = hasAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24;
                var texture2d = new Texture2D(tex.Width, tex.Height, format, true);
                texture2d.name = string.IsNullOrEmpty(tex.Name) ? $"texture_{i}" : CleanTextureName(tex.Name);
                texture2d.filterMode = FilterMode.Point;
                texture2d.wrapMode = TextureWrapMode.Repeat;

                var pixels = new Color32[tex.Width * tex.Height];
                for (int row = 0; row < tex.Height; row++)
                {
                    int srcRow = tex.Height - 1 - row;
                    for (int col = 0; col < tex.Width; col++)
                    {
                        int srcPixel = srcRow * tex.Width + col;
                        int dstPixel = row * tex.Width + col;
                        int srcIdx = srcPixel * 3;
                        if (srcIdx + 2 >= tex.PixelData.Length) continue;

                        byte r = tex.PixelData[srcIdx];
                        byte g = tex.PixelData[srcIdx + 1];
                        byte b = tex.PixelData[srcIdx + 2];
                        byte a = 255;

                        if (hasAlpha && tex.PaletteIndices != null && srcPixel < tex.PaletteIndices.Length)
                        {
                            if (tex.PaletteIndices[srcPixel] == 255)
                                a = 0;
                        }

                        pixels[dstPixel] = new Color32(r, g, b, a);
                    }
                }

                texture2d.SetPixels32(pixels);
                texture2d.Apply(true, true);
                ctx.AddObjectToAsset($"tex_{i}", texture2d);
                result.Add(texture2d);
            }

            return result;
        }

        private static Shader FindBestShader(MdlTextureFlags flags)
        {
            bool needsTransparency = (flags & MdlTextureFlags.Alpha) != 0 ||
                                     (flags & MdlTextureFlags.Masked) != 0 ||
                                     (flags & MdlTextureFlags.Additive) != 0;

            string[] shaderNames = needsTransparency
                ? new[] { "Universal Render Pipeline/Lit", "Standard" }
                : new[] { "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Lit", "Standard" };

            foreach (var name in shaderNames)
            {
                var shader = Shader.Find(name);
                if (shader != null) return shader;
            }

            return Shader.Find("Hidden/InternalErrorShader");
        }

        private static List<Material> CreateMaterials(
            AssetImportContext ctx,
            List<Texture2D> textures,
            IReadOnlyList<MdlParsedTexture> parsedTextures)
        {
            var materials = new List<Material>();
            if (parsedTextures == null) return materials;

            for (int i = 0; i < parsedTextures.Count; i++)
            {
                var ptex = parsedTextures[i];
                var flags = (MdlTextureFlags)ptex.Flags;
                var shader = FindBestShader(flags);
                var mat = new Material(shader);
                mat.name = i < textures.Count ? textures[i].name : $"mat_{i}";

                bool isUrp = shader.name.Contains("Universal Render Pipeline");

                if (i < textures.Count)
                {
                    mat.mainTexture = textures[i];
                    if (isUrp) mat.SetTexture("_BaseMap", textures[i]);
                }

                if (isUrp)
                    ConfigureUrpMaterial(mat, flags);
                else
                    ConfigureStandardMaterial(mat, flags);

                ctx.AddObjectToAsset($"mat_{i}", mat);
                materials.Add(mat);
            }

            return materials;
        }

        private static void ConfigureUrpMaterial(Material mat, MdlTextureFlags flags)
        {
            if ((flags & MdlTextureFlags.Additive) != 0)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 1f);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else if ((flags & MdlTextureFlags.Masked) != 0 || (flags & MdlTextureFlags.Alpha) != 0)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.renderQueue = 2450;
                mat.EnableKeyword("_ALPHATEST_ON");
            }

            if ((flags & MdlTextureFlags.Chrome) != 0)
            {
                mat.SetFloat("_Metallic", 0.8f);
                mat.SetFloat("_Smoothness", 0.6f);
            }

            if ((flags & MdlTextureFlags.FullBright) != 0)
            {
                mat.SetColor("_EmissionColor", Color.white);
                mat.EnableKeyword("_EMISSION");
            }
        }

        private static void ConfigureStandardMaterial(Material mat, MdlTextureFlags flags)
        {
            if ((flags & MdlTextureFlags.Additive) != 0)
            {
                mat.SetFloat("_Mode", 2f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
            }
            else if ((flags & MdlTextureFlags.Masked) != 0 || (flags & MdlTextureFlags.Alpha) != 0)
            {
                mat.SetFloat("_Mode", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;
            }

            if ((flags & MdlTextureFlags.Chrome) != 0)
            {
                mat.SetFloat("_Metallic", 0.8f);
                mat.SetFloat("_Glossiness", 0.6f);
            }

            if ((flags & MdlTextureFlags.FullBright) != 0)
            {
                mat.SetColor("_EmissionColor", Color.white);
                mat.EnableKeyword("_EMISSION");
            }
        }

        private static string CleanTextureName(string name)
        {
            int slashIdx = name.LastIndexOfAny(new[] { '/', '\\' });
            if (slashIdx >= 0) name = name.Substring(slashIdx + 1);
            int dotIdx = name.LastIndexOf('.');
            if (dotIdx >= 0) name = name.Substring(0, dotIdx);
            return name;
        }

        #endregion

        #region Mesh Building

        private static void ImportMeshes(
            AssetImportContext ctx,
            GameObject root,
            MdlParseResult result,
            Transform[] bones,
            List<Material> materials,
            GoldSrcMatrix[] boneMatricesGS)
        {
            if (result.ParsedBodyParts == null) return;

            int meshIdx = 0;
            foreach (var bodyPart in result.ParsedBodyParts)
            {
                foreach (var model in bodyPart.Models)
                {
                    if (model.Meshes == null || model.Meshes.Count == 0) continue;

                    // Pre-transform all vertices and normals from bone-local to bind-pose
                    // in GoldSrc space, then convert to Unity coordinates.
                    // This matches exactly what assimp's HL1MDLLoader does.
                    var bindPoseVerts = new Vector3[model.Vertices.Length];
                    var bindPoseNorms = new Vector3[model.Normals.Length];

                    for (int k = 0; k < model.Vertices.Length; k++)
                    {
                        int boneIdx = (model.VertexBoneIndices != null && k < model.VertexBoneIndices.Length)
                            ? model.VertexBoneIndices[k] : 0;

                        if (boneIdx >= 0 && boneIdx < boneMatricesGS.Length)
                        {
                            var v = model.Vertices[k];
                            var gsWorld = boneMatricesGS[boneIdx].TransformPoint(v.X, v.Y, v.Z);
                            bindPoseVerts[k] = GsToUnityPos(gsWorld);
                        }
                    }

                    for (int k = 0; k < model.Normals.Length; k++)
                    {
                        int boneIdx = (model.NormalBoneIndices != null && k < model.NormalBoneIndices.Length)
                            ? model.NormalBoneIndices[k] : 0;

                        if (boneIdx >= 0 && boneIdx < boneMatricesGS.Length)
                        {
                            var n = model.Normals[k];
                            var gsDir = boneMatricesGS[boneIdx].TransformDirection(n.X, n.Y, n.Z);
                            bindPoseNorms[k] = GsToUnityDir(gsDir).normalized;
                        }
                        else
                        {
                            bindPoseNorms[k] = Vector3.up;
                        }
                    }

                    var combinedMesh = BuildMesh(model, result, bones, bindPoseVerts, bindPoseNorms);
                    if (combinedMesh == null) continue;

                    string meshName = !string.IsNullOrEmpty(model.Name)
                        ? model.Name.TrimEnd('\0')
                        : $"mesh_{meshIdx}";
                    combinedMesh.name = meshName;

                    var meshObj = new GameObject(meshName);
                    meshObj.transform.SetParent(root.transform, false);

                    if (bones.Length > 0)
                    {
                        var smr = meshObj.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = combinedMesh;
                        smr.bones = bones;
                        smr.rootBone = bones[0].parent;

                        var mats = new Material[combinedMesh.subMeshCount];
                        for (int s = 0; s < combinedMesh.subMeshCount; s++)
                        {
                            int skinRef = s < model.Meshes.Count ? model.Meshes[s].MeshStruct.SkinRef : 0;
                            int texIdx = ResolveSkinRef(result, skinRef);
                            mats[s] = texIdx >= 0 && texIdx < materials.Count
                                ? materials[texIdx]
                                : (materials.Count > 0 ? materials[0] : null);
                        }
                        smr.sharedMaterials = mats;
                    }
                    else
                    {
                        var mf = meshObj.AddComponent<MeshFilter>();
                        mf.sharedMesh = combinedMesh;
                        var mr = meshObj.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = materials.Count > 0 ? materials[0] : null;
                    }

                    ctx.AddObjectToAsset($"mesh_{meshIdx}", combinedMesh);
                    meshIdx++;
                }
            }
        }

        private static Mesh BuildMesh(
            MdlParsedModel model,
            MdlParseResult result,
            Transform[] bones,
            Vector3[] bindPoseVerts,
            Vector3[] bindPoseNorms)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var boneWeights = new List<BoneWeight>();
            var subMeshTriangles = new List<List<int>>();

            int vertexOffset = 0;

            foreach (var parsedMesh in model.Meshes)
            {
                if (parsedMesh.Triangles == null || parsedMesh.Triangles.Count == 0)
                {
                    subMeshTriangles.Add(new List<int>());
                    continue;
                }

                int texWidth = 1, texHeight = 1;
                int texIdx = ResolveSkinRef(result, parsedMesh.MeshStruct.SkinRef);
                if (result.ParsedTextures != null && texIdx >= 0 && texIdx < result.ParsedTextures.Count)
                {
                    texWidth = Math.Max(1, result.ParsedTextures[texIdx].Width);
                    texHeight = Math.Max(1, result.ParsedTextures[texIdx].Height);
                }

                var indices = new List<int>();

                foreach (var tri in parsedMesh.Triangles)
                {
                    EmitVertex(model, tri.V0, texWidth, texHeight, bindPoseVerts, bindPoseNorms, vertices, normals, uvs, boneWeights);
                    EmitVertex(model, tri.V1, texWidth, texHeight, bindPoseVerts, bindPoseNorms, vertices, normals, uvs, boneWeights);
                    EmitVertex(model, tri.V2, texWidth, texHeight, bindPoseVerts, bindPoseNorms, vertices, normals, uvs, boneWeights);

                    int baseIdx = vertexOffset;
                    indices.Add(baseIdx);
                    indices.Add(baseIdx + 2);
                    indices.Add(baseIdx + 1);
                    vertexOffset += 3;
                }

                subMeshTriangles.Add(indices);
            }

            if (vertices.Count == 0) return null;

            var mesh = new Mesh();
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = boneWeights.ToArray();

            mesh.subMeshCount = subMeshTriangles.Count;
            for (int s = 0; s < subMeshTriangles.Count; s++)
                mesh.SetTriangles(subMeshTriangles[s], s);

            if (bones.Length > 0)
            {
                var bindPoses = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    bindPoses[i] = bones[i].worldToLocalMatrix * bones[0].parent.localToWorldMatrix;
                mesh.bindposes = bindPoses;
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EmitVertex(
            MdlParsedModel model,
            MdlTriVertex triVert,
            int texWidth, int texHeight,
            Vector3[] bindPoseVerts,
            Vector3[] bindPoseNorms,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<BoneWeight> boneWeights)
        {
            if (triVert.VertexIndex >= 0 && triVert.VertexIndex < bindPoseVerts.Length)
                vertices.Add(bindPoseVerts[triVert.VertexIndex]);
            else
                vertices.Add(Vector3.zero);

            if (triVert.NormalIndex >= 0 && triVert.NormalIndex < bindPoseNorms.Length)
                normals.Add(bindPoseNorms[triVert.NormalIndex]);
            else
                normals.Add(Vector3.up);

            float u = triVert.S / (float)texWidth;
            float v = 1f - (triVert.T / (float)texHeight);
            uvs.Add(new Vector2(u, v));

            int boneIdx = 0;
            if (model.VertexBoneIndices != null &&
                triVert.VertexIndex >= 0 &&
                triVert.VertexIndex < model.VertexBoneIndices.Length)
            {
                boneIdx = model.VertexBoneIndices[triVert.VertexIndex];
            }

            boneWeights.Add(new BoneWeight { boneIndex0 = boneIdx, weight0 = 1f });
        }

        #endregion

        private static int ResolveSkinRef(MdlParseResult result, int skinRef)
        {
            if (result.SkinRefTable != null && result.SkinRefTable.Length > 0
                && skinRef >= 0 && skinRef < result.NumSkinRef)
            {
                return result.SkinRefTable[skinRef];
            }
            return skinRef;
        }

        #region Animation Clips

        private static void ImportAnimations(
            AssetImportContext ctx,
            GameObject root,
            MdlParseResult result,
            Transform[] bones)
        {
            if (result.ParsedSequences == null || bones.Length == 0) return;

            var anim = root.AddComponent<Animation>();

            for (int seqIdx = 0; seqIdx < result.ParsedSequences.Count; seqIdx++)
            {
                var seq = result.ParsedSequences[seqIdx];
                if (seq.BoneFrames == null || seq.BoneFrames.Length == 0 || seq.NumFrames <= 0)
                    continue;

                var clip = new AnimationClip();
                clip.legacy = true;
                clip.name = string.IsNullOrEmpty(seq.Name) ? $"anim_{seqIdx}" : seq.Name;

                float frameDuration = seq.Fps > 0 ? 1f / seq.Fps : 1f / 30f;
                int numBones = Math.Min(bones.Length, seq.BoneFrames[0].Length);

                for (int boneIdx = 0; boneIdx < numBones; boneIdx++)
                {
                    var bone = result.Bones[boneIdx];
                    string bonePath = GetBonePath(bones, boneIdx);

                    var posX = new Keyframe[seq.NumFrames];
                    var posY = new Keyframe[seq.NumFrames];
                    var posZ = new Keyframe[seq.NumFrames];
                    var rotX = new Keyframe[seq.NumFrames];
                    var rotY = new Keyframe[seq.NumFrames];
                    var rotZ = new Keyframe[seq.NumFrames];
                    var rotW = new Keyframe[seq.NumFrames];

                    for (int f = 0; f < seq.NumFrames; f++)
                    {
                        float time = f * frameDuration;
                        var frame = seq.BoneFrames[f][boneIdx];

                        unsafe
                        {
                            float gx = bone.Value[0] + frame.Position.X * bone.Scale[0];
                            float gy = bone.Value[1] + frame.Position.Y * bone.Scale[1];
                            float gz = bone.Value[2] + frame.Position.Z * bone.Scale[2];

                            var pos = GoldSrcPositionToUnity(gx, gy, gz);
                            posX[f] = new Keyframe(time, pos.x);
                            posY[f] = new Keyframe(time, pos.y);
                            posZ[f] = new Keyframe(time, pos.z);

                            float rx = bone.Value[3] + frame.Rotation.X * bone.Scale[3];
                            float ry = bone.Value[4] + frame.Rotation.Y * bone.Scale[4];
                            float rz = bone.Value[5] + frame.Rotation.Z * bone.Scale[5];

                            var quat = GoldSrcRotationToUnity(rx, ry, rz);
                            rotX[f] = new Keyframe(time, quat.x);
                            rotY[f] = new Keyframe(time, quat.y);
                            rotZ[f] = new Keyframe(time, quat.z);
                            rotW[f] = new Keyframe(time, quat.w);
                        }
                    }

                    clip.SetCurve(bonePath, typeof(Transform), "localPosition.x", new AnimationCurve(posX));
                    clip.SetCurve(bonePath, typeof(Transform), "localPosition.y", new AnimationCurve(posY));
                    clip.SetCurve(bonePath, typeof(Transform), "localPosition.z", new AnimationCurve(posZ));
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation.x", new AnimationCurve(rotX));
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation.y", new AnimationCurve(rotY));
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation.z", new AnimationCurve(rotZ));
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation.w", new AnimationCurve(rotW));
                }

                clip.EnsureQuaternionContinuity();

                unsafe
                {
                    var seqDesc = seq.Descriptor;
                    if ((seqDesc.Flags & 0x0001) != 0)
                        clip.wrapMode = WrapMode.Loop;
                }

                ctx.AddObjectToAsset($"clip_{seqIdx}", clip);

                if (seqIdx == 0) anim.clip = clip;
                anim.AddClip(clip, clip.name);
            }
        }

        private static string GetBonePath(Transform[] bones, int boneIdx)
        {
            var parts = new List<string>();
            var current = bones[boneIdx];

            while (current != null)
            {
                if (current.name == "Armature")
                {
                    parts.Insert(0, "Armature");
                    break;
                }

                parts.Insert(0, current.name);
                current = current.parent;

                if (current != null && current.parent == null)
                    break;
            }

            return string.Join("/", parts);
        }

        #endregion

        #region Coordinate Conversion

        private static Vector3 GsToUnityPos(Vector3 gsWorld)
        {
            return new Vector3(-gsWorld.y * GoldSrcScale, gsWorld.z * GoldSrcScale, gsWorld.x * GoldSrcScale);
        }

        private static Vector3 GsToUnityDir(Vector3 gsDir)
        {
            return new Vector3(-gsDir.y, gsDir.z, gsDir.x);
        }

        private static Vector3 GoldSrcPositionToUnity(float gx, float gy, float gz)
        {
            return new Vector3(-gy * GoldSrcScale, gz * GoldSrcScale, gx * GoldSrcScale);
        }

        /// <summary>
        /// Converts GoldSrc Euler angles (radians) to a Unity quaternion.
        /// Matches Valve's AngleQuaternion exactly:
        ///   angles[0]=v3 (pitch), angles[1]=v4 (yaw), angles[2]=v5 (roll)
        ///   Quaternion order: Q = Qroll * Qyaw * Qpitch
        /// Then remaps axes GS(X,Y,Z) -> Unity(-Y,Z,X) with RH->LH conjugation.
        /// </summary>
        private static Quaternion GoldSrcRotationToUnity(float v3, float v4, float v5)
        {
            float hp = v3 * 0.5f, hy = v4 * 0.5f, hr = v5 * 0.5f;
            float sp = Mathf.Sin(hp), cp = Mathf.Cos(hp);
            float sy = Mathf.Sin(hy), cy = Mathf.Cos(hy);
            float sr = Mathf.Sin(hr), cr = Mathf.Cos(hr);

            float gx = sp * cy * cr - cp * sy * sr;
            float gy = cp * sy * cr + sp * cy * sr;
            float gz = cp * cy * sr - sp * sy * cr;
            float gw = cp * cy * cr + sp * sy * sr;

            return new Quaternion(gy, -gz, -gx, gw);
        }

        #endregion
    }
}
