using System;
using System.Collections.Generic;
using Source2Unity.Converters.Vmt;
using Source2Unity.Formats.Mdl;
using Source2Unity.Formats.Mdl.Parsers;
using Source2Unity.Formats.Mdl.Structures;
using UnityEngine;

namespace Source2Unity.Converters.Mdl
{
    /// <summary>
    /// Returned by <see cref="MdlModelBuilder.Build"/> so that callers (Editor importer
    /// or runtime loader) can manage the created Unity objects as needed.
    /// </summary>
    public sealed class MdlBuildResult
    {
        public GameObject Root { get; set; }
        public List<Texture2D> Textures { get; set; }
        public List<Material> Materials { get; set; }
        public List<Mesh> Meshes { get; set; }
        public List<AnimationClip> Clips { get; set; }
    }

    /// <summary>
    /// Converts a parsed <see cref="MdlParseResult"/> into a fully-assembled Unity GameObject
    /// with skinned meshes, bone hierarchy, textures, materials, and legacy animation clips.
    /// Usable from both Editor (ScriptedImporter) and Runtime (file-based loader).
    /// </summary>
    public static class MdlModelBuilder
    {
        public static MdlBuildResult Build(MdlParseResult result)
        {
            var root = new GameObject(result.ModelName ?? "MDL Model");

            var bones = BuildBoneHierarchy(root, result);
            ApplyReferencePose(result, bones);
            var textures = BuildTextures(result.ParsedTextures);
            var materials = BuildMaterials(textures, result.ParsedTextures);
            var meshes = BuildMeshes(root, result, bones, materials);
            var clips = BuildAnimations(root, result, bones);
            BuildAttachments(root, result, bones);
            BuildHitBoxes(result, bones);

            return new MdlBuildResult
            {
                Root = root,
                Textures = textures,
                Materials = materials,
                Meshes = meshes,
                Clips = clips,
            };
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
                    boneObj.transform.localPosition = GoldSrcCoordinates.PositionToUnity(
                        bone.Value[0], bone.Value[1], bone.Value[2]);
                    boneObj.transform.localRotation = GoldSrcCoordinates.RotationToUnity(
                        bone.Value[3], bone.Value[4], bone.Value[5]);
                }
            }

            return boneTransforms;
        }

        /// <summary>
        /// Corrects root bone yaw using sequence 0 frame 0.
        /// GoldSrc bone.Value[] can have a different yaw than what animations expect
        /// (typically ~90 deg offset on the root bone). Only the yaw component is extracted
        /// via swing-twist decomposition to preserve pitch/roll from the original T-pose.
        /// </summary>
        private static void ApplyReferencePose(MdlParseResult result, Transform[] bones)
        {
            if (bones.Length == 0 || result.ParsedSequences == null || result.ParsedSequences.Count == 0)
                return;

            var refSeq = result.ParsedSequences[0];
            if (refSeq.BoneFrames == null || refSeq.BoneFrames.Length == 0 || refSeq.NumFrames <= 0)
                return;

            var frame0 = refSeq.BoneFrames[0];

            for (int i = 0; i < bones.Length && i < frame0.Length; i++)
            {
                if (result.Bones[i].Parent >= 0) continue;

                var bone = result.Bones[i];
                var frame = frame0[i];

                unsafe
                {
                    var qBind = GoldSrcCoordinates.RotationToUnity(bone.Value[3], bone.Value[4], bone.Value[5]);

                    float rx = bone.Value[3] + frame.Rotation.X * bone.Scale[3];
                    float ry = bone.Value[4] + frame.Rotation.Y * bone.Scale[4];
                    float rz = bone.Value[5] + frame.Rotation.Z * bone.Scale[5];
                    var qRef = GoldSrcCoordinates.RotationToUnity(rx, ry, rz);

                    var qDiff = qRef * Quaternion.Inverse(qBind);

                    float len = Mathf.Sqrt(qDiff.y * qDiff.y + qDiff.w * qDiff.w);
                    if (len < 0.0001f)
                        continue;

                    var yawOnly = new Quaternion(0f, qDiff.y / len, 0f, qDiff.w / len);
                    bones[i].localRotation = yawOnly * qBind;
                }
            }
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

        #region Textures

        private static List<Texture2D> BuildTextures(IReadOnlyList<MdlParsedTexture> textures)
        {
            var result = new List<Texture2D>();
            if (textures == null) return result;

            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                var flags = (MdlTextureFlags)tex.Flags;
                bool hasAlpha = (flags & MdlTextureFlags.Alpha) != 0 ||
                                (flags & MdlTextureFlags.Masked) != 0;
                bool generateMips = (flags & MdlTextureFlags.NoMips) == 0;

                var format = hasAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24;
                var texture2d = new Texture2D(tex.Width, tex.Height, format, generateMips);
                texture2d.name = string.IsNullOrEmpty(tex.Name) ? $"texture_{i}" : CleanTextureName(tex.Name);
                texture2d.filterMode = generateMips ? FilterMode.Bilinear : FilterMode.Point;
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
                texture2d.Apply(generateMips, true);
                result.Add(texture2d);
            }

            return result;
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

        #region Materials

        private static List<Material> BuildMaterials(
            List<Texture2D> textures,
            IReadOnlyList<MdlParsedTexture> parsedTextures)
        {
            var materials = new List<Material>();
            if (parsedTextures == null) return materials;

            var shader = Source2UnityShaders.FindGoldSrc();

            for (int i = 0; i < parsedTextures.Count; i++)
            {
                var ptex = parsedTextures[i];
                var flags = (MdlTextureFlags)ptex.Flags;
                var mat = new Material(shader);
                mat.name = i < textures.Count ? textures[i].name : $"mat_{i}";

                if (i < textures.Count)
                {
                    mat.SetTexture("_BaseMap", textures[i]);
                    mat.mainTexture = textures[i];
                }

                Source2UnityShaders.ConfigureGoldSrcMaterial(mat, flags);
                materials.Add(mat);
            }

            return materials;
        }

        #endregion

        #region Meshes

        private static List<Mesh> BuildMeshes(
            GameObject root,
            MdlParseResult result,
            Transform[] bones,
            List<Material> materials)
        {
            var builtMeshes = new List<Mesh>();
            if (result.ParsedBodyParts == null) return builtMeshes;

            int meshIdx = 0;
            foreach (var bodyPart in result.ParsedBodyParts)
            {
                foreach (var model in bodyPart.Models)
                {
                    if (model.Meshes == null || model.Meshes.Count == 0) continue;

                    var bindPoseVerts = new Vector3[model.Vertices.Length];
                    var bindPoseNorms = new Vector3[model.Normals.Length];

                    for (int k = 0; k < model.Vertices.Length; k++)
                    {
                        int boneIdx = (model.VertexBoneIndices != null && k < model.VertexBoneIndices.Length)
                            ? model.VertexBoneIndices[k] : 0;

                        if (boneIdx >= 0 && boneIdx < bones.Length)
                        {
                            var v = model.Vertices[k];
                            var localUnity = GoldSrcCoordinates.PositionToUnity(v.X, v.Y, v.Z);
                            bindPoseVerts[k] = bones[boneIdx].TransformPoint(localUnity);
                        }
                    }

                    for (int k = 0; k < model.Normals.Length; k++)
                    {
                        int boneIdx = (model.NormalBoneIndices != null && k < model.NormalBoneIndices.Length)
                            ? model.NormalBoneIndices[k] : 0;

                        if (boneIdx >= 0 && boneIdx < bones.Length)
                        {
                            var n = model.Normals[k];
                            var localNormUnity = new Vector3(-n.Y, n.Z, n.X);
                            bindPoseNorms[k] = bones[boneIdx].TransformDirection(localNormUnity).normalized;
                        }
                        else
                        {
                            bindPoseNorms[k] = Vector3.up;
                        }
                    }

                    var combinedMesh = AssembleMesh(model, result, bones, bindPoseVerts, bindPoseNorms);
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

                    builtMeshes.Add(combinedMesh);
                    meshIdx++;
                }
            }

            return builtMeshes;
        }

        private static Mesh AssembleMesh(
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

                bool isFlatShade = false;
                if (result.ParsedTextures != null && texIdx >= 0 && texIdx < result.ParsedTextures.Count)
                    isFlatShade = (result.ParsedTextures[texIdx].Flags & (int)MdlTextureFlags.FlatShade) != 0;

                var indices = new List<int>();

                foreach (var tri in parsedMesh.Triangles)
                {
                    EmitVertex(model, tri.V0, texWidth, texHeight, bindPoseVerts, bindPoseNorms, vertices, normals, uvs, boneWeights);
                    EmitVertex(model, tri.V1, texWidth, texHeight, bindPoseVerts, bindPoseNorms, vertices, normals, uvs, boneWeights);
                    EmitVertex(model, tri.V2, texWidth, texHeight, bindPoseVerts, bindPoseNorms, vertices, normals, uvs, boneWeights);

                    if (isFlatShade)
                    {
                        var v0 = vertices[vertexOffset];
                        var v1 = vertices[vertexOffset + 1];
                        var v2 = vertices[vertexOffset + 2];
                        var faceNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                        normals[vertexOffset] = faceNormal;
                        normals[vertexOffset + 1] = faceNormal;
                        normals[vertexOffset + 2] = faceNormal;
                    }

                    int baseIdx = vertexOffset;
                    indices.Add(baseIdx);
                    indices.Add(baseIdx + 1);
                    indices.Add(baseIdx + 2);
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

        #region Animations

        private static List<AnimationClip> BuildAnimations(
            GameObject root,
            MdlParseResult result,
            Transform[] bones)
        {
            var clips = new List<AnimationClip>();
            if (result.ParsedSequences == null || bones.Length == 0) return clips;

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
                            var pos = GoldSrcCoordinates.PositionToUnity(
                                bone.Value[0] + frame.Position.X * bone.Scale[0],
                                bone.Value[1] + frame.Position.Y * bone.Scale[1],
                                bone.Value[2] + frame.Position.Z * bone.Scale[2]);
                            posX[f] = new Keyframe(time, pos.x);
                            posY[f] = new Keyframe(time, pos.y);
                            posZ[f] = new Keyframe(time, pos.z);

                            float rx = bone.Value[3] + frame.Rotation.X * bone.Scale[3];
                            float ry = bone.Value[4] + frame.Rotation.Y * bone.Scale[4];
                            float rz = bone.Value[5] + frame.Rotation.Z * bone.Scale[5];

                            var quat = GoldSrcCoordinates.RotationToUnity(rx, ry, rz);
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

                if (seq.Events != null && seq.Events.Count > 0)
                {
                    var animEvents = new AnimationEvent[seq.Events.Count];
                    for (int e = 0; e < seq.Events.Count; e++)
                    {
                        var ev = seq.Events[e];
                        animEvents[e] = new AnimationEvent
                        {
                            time = seq.NumFrames > 1
                                ? (float)ev.Frame / (seq.NumFrames - 1)
                                : 0f,
                            intParameter = ev.EventId,
                            stringParameter = ev.Options ?? string.Empty,
                            functionName = $"OnAnimEvent_{ev.EventId}"
                        };
                    }
                    clip.events = animEvents;
                }

                unsafe
                {
                    var seqDesc = seq.Descriptor;
                    if ((seqDesc.Flags & 0x0001) != 0)
                        clip.wrapMode = WrapMode.Loop;
                }

                clips.Add(clip);

                if (seqIdx == 0) anim.clip = clip;
                anim.AddClip(clip, clip.name);
            }

            return clips;
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

        #region Attachments and HitBoxes

        private static unsafe void BuildAttachments(GameObject root, MdlParseResult result, Transform[] bones)
        {
            if (result.Attachments == null || result.Attachments.Count == 0 || bones.Length == 0) return;

            for (int i = 0; i < result.Attachments.Count; i++)
            {
                var att = result.Attachments[i];
                byte* ptr = att.Name;
                int len = 0;
                while (len < 32 && ptr[len] != 0) len++;
                string attName = len > 0
                    ? System.Text.Encoding.ASCII.GetString(ptr, len)
                    : $"attachment_{i}";

                var obj = new GameObject(attName);

                int boneIdx = att.Bone;
                if (boneIdx >= 0 && boneIdx < bones.Length)
                    obj.transform.SetParent(bones[boneIdx], false);
                else
                    obj.transform.SetParent(root.transform, false);

                obj.transform.localPosition = GoldSrcCoordinates.PositionToUnity(att.Org.X, att.Org.Y, att.Org.Z);
            }
        }

        private static void BuildHitBoxes(MdlParseResult result, Transform[] bones)
        {
            if (result.HitBoxes == null || result.HitBoxes.Count == 0 || bones.Length == 0) return;

            for (int i = 0; i < result.HitBoxes.Count; i++)
            {
                var hb = result.HitBoxes[i];
                int boneIdx = hb.Bone;
                if (boneIdx < 0 || boneIdx >= bones.Length) continue;

                var col = bones[boneIdx].gameObject.AddComponent<BoxCollider>();

                var min = GoldSrcCoordinates.PositionToUnity(hb.BbMin.X, hb.BbMin.Y, hb.BbMin.Z);
                var max = GoldSrcCoordinates.PositionToUnity(hb.BbMax.X, hb.BbMax.Y, hb.BbMax.Z);

                col.center = (min + max) * 0.5f;
                col.size = new Vector3(
                    Mathf.Abs(max.x - min.x),
                    Mathf.Abs(max.y - min.y),
                    Mathf.Abs(max.z - min.z));
            }
        }

        #endregion

        #region Utilities

        private static int ResolveSkinRef(MdlParseResult result, int skinRef)
        {
            if (result.SkinRefTable != null && result.SkinRefTable.Length > 0
                && skinRef >= 0 && skinRef < result.NumSkinRef)
            {
                return result.SkinRefTable[skinRef];
            }
            return skinRef;
        }

        #endregion
    }
}
