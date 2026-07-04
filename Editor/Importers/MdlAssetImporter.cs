using System;
using System.Collections.Generic;
using Source2Unity.Formats.Mdl;
using Source2Unity.Formats.Mdl.Parsers;
using Source2Unity.Formats.Mdl.Structures;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Source2Unity.Editor.Importers
{
    [ScriptedImporter(1, "mdl")]
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

                var bones = BuildBoneHierarchy(root, result);
                var textures = ImportTextures(ctx, result.ParsedTextures);
                var materials = CreateMaterials(ctx, textures, result.ParsedTextures);
                ImportSkinnedMeshes(ctx, root, result, bones, materials);
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

        #region Bone Hierarchy

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
                for (int p = 0; p < pixels.Length; p++)
                {
                    int srcIdx = p * 3;
                    if (srcIdx + 2 >= tex.PixelData.Length) break;

                    byte r = tex.PixelData[srcIdx];
                    byte g = tex.PixelData[srcIdx + 1];
                    byte b = tex.PixelData[srcIdx + 2];
                    byte a = 255;

                    if (hasAlpha && tex.PaletteIndices != null && p < tex.PaletteIndices.Length)
                    {
                        if (tex.PaletteIndices[p] == 255)
                            a = 0;
                    }

                    pixels[p] = new Color32(r, g, b, a);
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
                    if (isUrp)
                        mat.SetTexture("_BaseMap", textures[i]);
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
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 1f); // Additive
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.renderQueue = 3000;
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else if ((flags & MdlTextureFlags.Masked) != 0 || (flags & MdlTextureFlags.Alpha) != 0)
            {
                mat.SetFloat("_Surface", 0f); // Opaque
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

        #region Skinned Meshes

        private static void ImportSkinnedMeshes(
            AssetImportContext ctx,
            GameObject root,
            MdlParseResult result,
            Transform[] bones,
            List<Material> materials)
        {
            if (result.ParsedBodyParts == null) return;

            int meshIdx = 0;
            foreach (var bodyPart in result.ParsedBodyParts)
            {
                foreach (var model in bodyPart.Models)
                {
                    if (model.Meshes == null || model.Meshes.Count == 0) continue;

                    var combinedMesh = BuildSkinnedMesh(model, result, bones, root.transform);
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
                            mats[s] = skinRef >= 0 && skinRef < materials.Count
                                ? materials[skinRef]
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

        private static Mesh BuildSkinnedMesh(
            MdlParsedModel model,
            MdlParseResult result,
            Transform[] bones,
            Transform rootTransform)
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
                int skinRef = parsedMesh.MeshStruct.SkinRef;
                if (result.ParsedTextures != null && skinRef >= 0 && skinRef < result.ParsedTextures.Count)
                {
                    texWidth = Math.Max(1, result.ParsedTextures[skinRef].Width);
                    texHeight = Math.Max(1, result.ParsedTextures[skinRef].Height);
                }

                var indices = new List<int>();

                foreach (var tri in parsedMesh.Triangles)
                {
                    AddSkinnedVertex(model, tri.V0, texWidth, texHeight, vertices, normals, uvs, boneWeights);
                    AddSkinnedVertex(model, tri.V1, texWidth, texHeight, vertices, normals, uvs, boneWeights);
                    AddSkinnedVertex(model, tri.V2, texWidth, texHeight, vertices, normals, uvs, boneWeights);

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
                var bindPoses = ComputeBindPoses(bones, rootTransform);
                mesh.bindposes = bindPoses;
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static Matrix4x4[] ComputeBindPoses(Transform[] bones, Transform rootTransform)
        {
            var bindPoses = new Matrix4x4[bones.Length];
            var worldMatrices = new Matrix4x4[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                worldMatrices[i] = bones[i].localToWorldMatrix;
                bindPoses[i] = worldMatrices[i].inverse * rootTransform.localToWorldMatrix;
            }

            return bindPoses;
        }

        private static void AddSkinnedVertex(
            MdlParsedModel model,
            MdlTriVertex triVert,
            int texWidth,
            int texHeight,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<BoneWeight> boneWeights)
        {
            if (triVert.VertexIndex >= 0 && triVert.VertexIndex < model.Vertices.Length)
            {
                vertices.Add(GoldSrcToUnity(model.Vertices[triVert.VertexIndex]));
            }
            else
            {
                vertices.Add(Vector3.zero);
            }

            if (triVert.NormalIndex >= 0 && triVert.NormalIndex < model.Normals.Length)
            {
                normals.Add(GoldSrcToUnityDir(model.Normals[triVert.NormalIndex]));
            }
            else
            {
                normals.Add(Vector3.up);
            }

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

            boneWeights.Add(new BoneWeight
            {
                boneIndex0 = boneIdx,
                weight0 = 1f
            });
        }

        #endregion

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

                if (seqIdx == 0)
                    anim.clip = clip;
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

        private static Vector3 GoldSrcToUnity(Vector3F v)
        {
            return new Vector3(-v.Y * GoldSrcScale, v.Z * GoldSrcScale, v.X * GoldSrcScale);
        }

        private static Vector3 GoldSrcToUnityDir(Vector3F v)
        {
            return new Vector3(-v.Y, v.Z, v.X);
        }

        private static Vector3 GoldSrcPositionToUnity(float gx, float gy, float gz)
        {
            return new Vector3(-gy * GoldSrcScale, gz * GoldSrcScale, gx * GoldSrcScale);
        }

        private static Quaternion GoldSrcRotationToUnity(float v3, float v4, float v5)
        {
            float hx = v4 * 0.5f, hy = v3 * 0.5f, hz = v5 * 0.5f;
            float cx = Mathf.Cos(hx), sx = Mathf.Sin(hx);
            float cy = Mathf.Cos(hy), sy = Mathf.Sin(hy);
            float cz = Mathf.Cos(hz), sz = Mathf.Sin(hz);

            float gw = cz * cy * cx + sz * sy * sx;
            float gx = cz * cy * sx - sz * sy * cx;
            float gy = cz * sy * cx + sz * cy * sx;
            float gz = sz * cy * cx - cz * sy * sx;

            return new Quaternion(gy, -gz, -gx, gw);
        }

        #endregion
    }
}
