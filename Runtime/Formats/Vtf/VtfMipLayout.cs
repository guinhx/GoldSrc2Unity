using System;
using System.IO;

namespace Source2Unity.Formats.Vtf
{
    public static class VtfMipLayout
    {
        public const int StandardCubemapFaces = 6;

        public static int ComputeMipDataSize(VtfImageFormat format, int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            return format switch
            {
                VtfImageFormat.Dxt1 or VtfImageFormat.Dxt1OneBitAlpha
                    => Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8,
                VtfImageFormat.Dxt3 or VtfImageFormat.Dxt5
                    => Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 16,
                VtfImageFormat.Rgba8888 or VtfImageFormat.Abgr8888 or VtfImageFormat.Argb8888 or VtfImageFormat.Bgra8888 or VtfImageFormat.Bgrx8888
                    => width * height * 4,
                VtfImageFormat.Rgb888 or VtfImageFormat.Bgr888
                    => width * height * 3,
                _ => throw new NotSupportedException($"VTF image format not supported: {format}")
            };
        }

        public static int ComputeThumbnailSize(int thumbWidth, int thumbHeight)
        {
            if (thumbWidth <= 0 || thumbHeight <= 0)
                return 0;

            return ComputeMipDataSize(VtfImageFormat.Dxt1, thumbWidth, thumbHeight);
        }

        public static int ComputeTotalImageChainSize(VtfImageFormat format, int width, int height, int mipCount, int frames, int faces, int depth)
        {
            mipCount = Math.Max(1, mipCount);
            frames = Math.Max(1, frames);
            faces = Math.Max(1, faces);
            depth = Math.Max(1, depth);

            int total = 0;
            for (int mip = mipCount - 1; mip >= 0; mip--)
            {
                int w = Math.Max(1, width >> mip);
                int h = Math.Max(1, height >> mip);
                int mipSize = ComputeMipDataSize(format, w, h);
                total += mipSize * frames * faces * depth;
            }

            return total;
        }

        public static byte[] ExtractBaseMip(
            byte[] chain,
            VtfImageFormat format,
            int width,
            int height,
            int mipLevels,
            int frames,
            int faces,
            int depth)
        {
            return ExtractSlice(chain, format, width, height, mipLevels, frames, faces, depth,
                frame: 0, face: 0, depthSlice: 0, mipLevel: 0);
        }

        public static byte[] ExtractSlice(
            byte[] chain,
            VtfImageFormat format,
            int width,
            int height,
            int mipLevels,
            int frameCount,
            int faceCount,
            int depthCount,
            int frame,
            int face,
            int depthSlice,
            int mipLevel)
        {
            if (chain == null)
                throw new ArgumentNullException(nameof(chain));

            int offset = ComputeSliceOffset(format, width, height, mipLevels, frameCount, faceCount, depthCount,
                frame, face, depthSlice, mipLevel);

            int mipW = Math.Max(1, width >> mipLevel);
            int mipH = Math.Max(1, height >> mipLevel);
            int mipSize = ComputeMipDataSize(format, mipW, mipH);

            if (offset + mipSize > chain.Length)
                throw new InvalidDataException("VTF slice read exceeds image chain bounds.");

            var slice = new byte[mipSize];
            Buffer.BlockCopy(chain, offset, slice, 0, mipSize);
            return slice;
        }

        public static int ComputeSliceOffset(
            VtfImageFormat format,
            int width,
            int height,
            int mipLevels,
            int frameCount,
            int faceCount,
            int depthCount,
            int frame,
            int face,
            int depthSlice,
            int mipLevel)
        {
            mipLevels = Math.Max(1, mipLevels);
            frameCount = Math.Max(1, frameCount);
            faceCount = Math.Max(1, faceCount);
            depthCount = Math.Max(1, depthCount);
            mipLevel = Math.Clamp(mipLevel, 0, mipLevels - 1);

            int offset = 0;
            for (int mip = mipLevels - 1; mip > mipLevel; mip--)
            {
                int w = Math.Max(1, width >> mip);
                int h = Math.Max(1, height >> mip);
                offset += ComputeMipDataSize(format, w, h) * frameCount * faceCount * depthCount;
            }

            int mipW = Math.Max(1, width >> mipLevel);
            int mipH = Math.Max(1, height >> mipLevel);
            int mipSize = ComputeMipDataSize(format, mipW, mipH);
            offset += frame * mipSize * faceCount * depthCount;
            offset += face * mipSize * depthCount;
            offset += depthSlice * mipSize;
            return offset;
        }
    }
}
