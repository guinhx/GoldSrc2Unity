using System;
using Source2Unity.Formats.Vtf;
using Source2Unity.Formats.Vtf.Parsers;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Source2Unity.Converters.Vtf
{
    public sealed class VtfTextureBuildOptions
    {
        public static VtfTextureBuildOptions Default { get; } = new VtfTextureBuildOptions();

        public bool Linear { get; init; }
        public bool BuildCubemap { get; init; } = true;
        public bool BuildAnimationFrames { get; init; } = true;
        public bool BuildVolume { get; init; } = true;
        public bool PackAnimationIntoSpriteSheet { get; init; }
        public Action<string> OnWarning { get; init; }
    }

    public sealed class VtfBuildResult
    {
        public Texture2D Texture { get; init; }
        public Cubemap Cubemap { get; init; }
        public Texture3D VolumeTexture { get; init; }
        public Texture2D[] AnimationFrames { get; init; }
        public Texture2D SpriteSheet { get; init; }
        public float SuggestedFrameRate { get; init; } = 15f;
        public bool IsCubemap { get; init; }
        public bool IsAnimated { get; init; }
        public bool IsVolume { get; init; }
    }

    public static class VtfTextureBuilder
    {
        public static VtfBuildResult Build(VtfParseResult result, VtfTextureBuildOptions options = null)
        {
            options ??= VtfTextureBuildOptions.Default;
            if (result?.ImageChain == null)
                throw new ArgumentException("VTF parse result has no image chain.", nameof(result));

            Cubemap cubemap = null;
            Texture3D volume = null;
            Texture2D[] frames = null;
            Texture2D spriteSheet = null;

            if (result.IsCubemap && options.BuildCubemap)
                cubemap = BuildCubemap(result, options);

            if (result.IsVolume && !result.IsCubemap && options.BuildVolume)
                volume = BuildVolumeTexture(result, options);

            if (result.IsAnimated && !result.IsCubemap && !result.IsVolume && options.BuildAnimationFrames)
            {
                frames = BuildAnimationFrames(result, options);
                if (options.PackAnimationIntoSpriteSheet && frames.Length > 1)
                    spriteSheet = PackFramesHorizontally(frames, options.Linear);
            }

            Texture2D preview = BuildPreviewTexture(result, options, cubemap, volume, frames);

            return new VtfBuildResult
            {
                Texture = preview,
                Cubemap = cubemap,
                VolumeTexture = volume,
                AnimationFrames = frames,
                SpriteSheet = spriteSheet,
                SuggestedFrameRate = EstimateFrameRate(result),
                IsCubemap = result.IsCubemap,
                IsAnimated = result.IsAnimated && !result.IsCubemap && !result.IsVolume,
                IsVolume = result.IsVolume && volume != null
            };
        }

        public static VtfBuildResult Build(VtfParseResult result, bool linear)
        {
            return Build(result, new VtfTextureBuildOptions { Linear = linear });
        }

        private static Texture2D BuildPreviewTexture(
            VtfParseResult result,
            VtfTextureBuildOptions options,
            Cubemap cubemap,
            Texture3D volume,
            Texture2D[] frames)
        {
            if (frames != null && frames.Length > 0)
                return frames[0];

            if (cubemap != null)
                return ExtractCubemapFaceTexture(result, options, face: 0);

            if (volume != null)
            {
                int midSlice = Math.Max(0, result.Depth / 2);
                return CreateTextureFromSlice(result, options, frame: 0, face: 0, depthSlice: midSlice);
            }

            return CreateTextureFromSlice(result, options, frame: 0, face: 0, depthSlice: 0);
        }

        private static Cubemap BuildCubemap(VtfParseResult result, VtfTextureBuildOptions options)
        {
            int faceCount = Math.Min(VtfMipLayout.StandardCubemapFaces, result.FaceCount);
            if (result.FaceCount > VtfMipLayout.StandardCubemapFaces)
            {
                options.OnWarning?.Invoke(
                    $"VTF has {result.FaceCount} faces (includes spheremap); using first {VtfMipLayout.StandardCubemapFaces} cubemap faces.");
            }

            var cubemap = new Cubemap(result.Width, DefaultFormat.LDR, TextureCreationFlags.None);
            for (int face = 0; face < faceCount; face++)
            {
                byte[] rgba = DecodeSlice(result, frame: 0, face, depthSlice: 0);
                cubemap.SetPixelData(rgba, mipLevel: 0, (CubemapFace)face);
            }

            cubemap.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return cubemap;
        }

        private static Texture3D BuildVolumeTexture(VtfParseResult result, VtfTextureBuildOptions options)
        {
            int width = result.Width;
            int height = result.Height;
            int depth = result.Depth;
            int pixelCount = width * height;
            var volumeData = new byte[pixelCount * depth * 4];

            for (int slice = 0; slice < depth; slice++)
            {
                byte[] rgba = DecodeSlice(result, frame: 0, face: 0, depthSlice: slice);
                int copyLength = Math.Min(rgba.Length, pixelCount * 4);
                Buffer.BlockCopy(rgba, 0, volumeData, slice * pixelCount * 4, copyLength);
            }

            var volume = new Texture3D(width, height, depth, DefaultFormat.LDR, TextureCreationFlags.None)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            volume.SetPixelData(volumeData, mipLevel: 0);
            volume.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return volume;
        }

        private static Texture2D[] BuildAnimationFrames(VtfParseResult result, VtfTextureBuildOptions options)
        {
            var frames = new Texture2D[result.FrameCount];
            for (int frame = 0; frame < result.FrameCount; frame++)
                frames[frame] = CreateTextureFromSlice(result, options, frame, face: 0, depthSlice: 0);

            return frames;
        }

        private static Texture2D ExtractCubemapFaceTexture(VtfParseResult result, VtfTextureBuildOptions options, int face)
        {
            return CreateTextureFromSlice(result, options, frame: 0, face, depthSlice: 0);
        }

        private static Texture2D CreateTextureFromSlice(
            VtfParseResult result,
            VtfTextureBuildOptions options,
            int frame,
            int face,
            int depthSlice)
        {
            byte[] rgba = DecodeSlice(result, frame, face, depthSlice);
            var texture = new Texture2D(result.Width, result.Height, TextureFormat.RGBA32, mipChain: false, options.Linear);
            texture.LoadRawTextureData(rgba);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private static byte[] DecodeSlice(VtfParseResult result, int frame, int face, int depthSlice)
        {
            byte[] mip = VtfMipLayout.ExtractSlice(
                result.ImageChain,
                result.Format,
                result.Width,
                result.Height,
                result.MipCount,
                result.FrameCount,
                result.FaceCount,
                result.Depth,
                frame,
                face,
                depthSlice,
                mipLevel: 0);

            return VtfFormatDecoder.DecodeToRgba32(mip, result.Format, result.Width, result.Height);
        }

        private static Texture2D PackFramesHorizontally(Texture2D[] frames, bool linear)
        {
            int frameWidth = frames[0].width;
            int frameHeight = frames[0].height;
            var sheet = new Texture2D(frameWidth * frames.Length, frameHeight, TextureFormat.RGBA32, false, linear);

            for (int i = 0; i < frames.Length; i++)
                sheet.SetPixels(i * frameWidth, 0, frameWidth, frameHeight, frames[i].GetPixels());

            sheet.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return sheet;
        }

        private static float EstimateFrameRate(VtfParseResult result)
        {
            if (!result.IsAnimated)
                return 15f;

            return result.FrameCount switch
            {
                <= 10 => 10f,
                <= 30 => 15f,
                <= 60 => 24f,
                _ => 30f
            };
        }
    }
}
