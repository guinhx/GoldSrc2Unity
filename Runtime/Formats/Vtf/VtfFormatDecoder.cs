using System;
using Source2Unity.Formats.Vtf.Parsers;

namespace Source2Unity.Formats.Vtf
{
    /// <summary>
    /// Software decoder for common VTF image formats → RGBA32 byte buffer (width × height × 4).
    /// </summary>
    public static class VtfFormatDecoder
    {
        public static byte[] DecodeToRgba32(VtfParseResult result)
        {
            if (result?.BaseMipData == null)
                throw new ArgumentNullException(nameof(result));

            return DecodeToRgba32(result.BaseMipData, result.Format, result.Width, result.Height);
        }

        public static byte[] DecodeToRgba32(byte[] mipData, VtfImageFormat format, int width, int height)
        {
            if (mipData == null)
                throw new ArgumentNullException(nameof(mipData));

            return format switch
            {
                VtfImageFormat.Rgba8888 => CopyRgba8888(mipData, format, width, height),
                VtfImageFormat.Bgra8888 or VtfImageFormat.Bgrx8888 => CopyBgra8888(mipData, format, width, height),
                VtfImageFormat.Rgb888 => CopyRgb888(mipData, width, height, swapBgr: false),
                VtfImageFormat.Bgr888 => CopyRgb888(mipData, width, height, swapBgr: true),
                VtfImageFormat.Dxt1 or VtfImageFormat.Dxt1OneBitAlpha => DecodeDxt1(mipData, width, height),
                VtfImageFormat.Dxt5 => DecodeDxt5(mipData, width, height),
                _ => throw new NotSupportedException($"VTF decode not supported for format: {format}")
            };
        }

        private static byte[] CopyRgba8888(byte[] mipData, VtfImageFormat format, int width, int height)
        {
            int size = width * height * 4;
            var output = new byte[size];
            Buffer.BlockCopy(mipData, 0, output, 0, Math.Min(size, mipData.Length));
            return output;
        }

        private static byte[] CopyBgra8888(byte[] mipData, VtfImageFormat format, int width, int height)
        {
            int pixelCount = width * height;
            var output = new byte[pixelCount * 4];
            int srcLen = Math.Min(pixelCount * 4, mipData.Length);

            for (int i = 0; i < srcLen; i += 4)
            {
                output[i] = mipData[i + 2];
                output[i + 1] = mipData[i + 1];
                output[i + 2] = mipData[i];
                output[i + 3] = format == VtfImageFormat.Bgrx8888 ? (byte)255 : mipData[i + 3];
            }

            return output;
        }

        private static byte[] CopyRgb888(byte[] mipData, int width, int height, bool swapBgr)
        {
            int pixelCount = width * height;
            var output = new byte[pixelCount * 4];
            int srcLen = Math.Min(pixelCount * 3, mipData.Length);

            for (int i = 0, o = 0; i + 2 < srcLen; i += 3, o += 4)
            {
                byte c0 = mipData[i];
                byte c1 = mipData[i + 1];
                byte c2 = mipData[i + 2];
                output[o] = swapBgr ? c2 : c0;
                output[o + 1] = c1;
                output[o + 2] = swapBgr ? c0 : c2;
                output[o + 3] = 255;
            }

            return output;
        }

        private static byte[] DecodeDxt1(byte[] mipData, int width, int height)
        {
            int blockWidth = Math.Max(1, (width + 3) / 4);
            int blockHeight = Math.Max(1, (height + 3) / 4);
            var output = new byte[width * height * 4];

            int blockIndex = 0;
            for (int by = 0; by < blockHeight; by++)
            {
                for (int bx = 0; bx < blockWidth; bx++)
                {
                    int blockOffset = blockIndex * 8;
                    if (blockOffset + 8 > mipData.Length)
                        return output;

                    DecodeDxt1Block(mipData, blockOffset, output, width, height, bx * 4, by * 4);
                    blockIndex++;
                }
            }

            return output;
        }

        private static byte[] DecodeDxt5(byte[] mipData, int width, int height)
        {
            int blockWidth = Math.Max(1, (width + 3) / 4);
            int blockHeight = Math.Max(1, (height + 3) / 4);
            var output = new byte[width * height * 4];

            int blockIndex = 0;
            for (int by = 0; by < blockHeight; by++)
            {
                for (int bx = 0; bx < blockWidth; bx++)
                {
                    int blockOffset = blockIndex * 16;
                    if (blockOffset + 16 > mipData.Length)
                        return output;

                    DecodeDxt5Block(mipData, blockOffset, output, width, height, bx * 4, by * 4);
                    blockIndex++;
                }
            }

            return output;
        }

        private static void DecodeDxt1Block(byte[] data, int offset, byte[] output, int width, int height, int blockX, int blockY)
        {
            ushort c0 = (ushort)(data[offset] | (data[offset + 1] << 8));
            ushort c1 = (ushort)(data[offset + 2] | (data[offset + 3] << 8));
            uint indices = (uint)(data[offset + 4] | (data[offset + 5] << 8) | (data[offset + 6] << 16) | (data[offset + 7] << 24));

            var colors = new uint[4];
            colors[0] = Rgb565ToRgba(c0, 255);
            colors[1] = Rgb565ToRgba(c1, 255);

            if (c0 > c1)
            {
                colors[2] = LerpRgb565(c0, c1, 1, 2, 255);
                colors[3] = LerpRgb565(c0, c1, 2, 1, 255);
            }
            else
            {
                colors[2] = LerpRgb565(c0, c1, 1, 1, 255);
                colors[3] = 0;
            }

            WriteBlockPixels(output, width, height, blockX, blockY, indices, colors);
        }

        private static void DecodeDxt5Block(byte[] data, int offset, byte[] output, int width, int height, int blockX, int blockY)
        {
            byte a0 = data[offset];
            byte a1 = data[offset + 1];
            ulong alphaIndices = 0;
            for (int i = 0; i < 6; i++)
                alphaIndices |= (ulong)data[offset + 2 + i] << (8 * i);

            var alphas = new byte[8];
            alphas[0] = a0;
            alphas[1] = a1;
            if (a0 > a1)
            {
                for (int i = 1; i <= 6; i++)
                    alphas[i + 1] = (byte)((a0 * (7 - i) + a1 * i) / 7);
            }
            else
            {
                for (int i = 1; i <= 4; i++)
                    alphas[i + 1] = (byte)((a0 * (5 - i) + a1 * i) / 5);
                alphas[6] = 0;
                alphas[7] = 255;
            }

            ushort c0 = (ushort)(data[offset + 8] | (data[offset + 9] << 8));
            ushort c1 = (ushort)(data[offset + 10] | (data[offset + 11] << 8));
            uint colorIndices = (uint)(data[offset + 12] | (data[offset + 13] << 8) | (data[offset + 14] << 16) | (data[offset + 15] << 24));

            var colors = new uint[4];
            colors[0] = Rgb565ToRgba(c0, 255);
            colors[1] = Rgb565ToRgba(c1, 255);
            colors[2] = LerpRgb565(c0, c1, 1, 2, 255);
            colors[3] = LerpRgb565(c0, c1, 2, 1, 255);

            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int x = blockX + px;
                    int y = blockY + py;
                    if (x >= width || y >= height)
                        continue;

                    int pixelIndex = py * 4 + px;
                    int colorCode = (int)((colorIndices >> (pixelIndex * 2)) & 3);
                    int alphaCode = (int)((alphaIndices >> (pixelIndex * 3)) & 7);

                    uint rgba = colors[colorCode];
                    byte alpha = alphas[alphaCode];
                    WritePixel(output, width, x, y, (byte)(rgba & 0xFF), (byte)((rgba >> 8) & 0xFF), (byte)((rgba >> 16) & 0xFF), alpha);
                }
            }
        }

        private static void WriteBlockPixels(byte[] output, int width, int height, int blockX, int blockY, uint indices, uint[] colors)
        {
            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int x = blockX + px;
                    int y = blockY + py;
                    if (x >= width || y >= height)
                        continue;

                    int code = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                    uint rgba = colors[code];
                    WritePixel(output, width, x, y,
                        (byte)(rgba & 0xFF),
                        (byte)((rgba >> 8) & 0xFF),
                        (byte)((rgba >> 16) & 0xFF),
                        (byte)((rgba >> 24) & 0xFF));
                }
            }
        }

        private static void WritePixel(byte[] output, int width, int x, int y, byte r, byte g, byte b, byte a)
        {
            int i = (y * width + x) * 4;
            output[i] = r;
            output[i + 1] = g;
            output[i + 2] = b;
            output[i + 3] = a;
        }

        private static uint Rgb565ToRgba(ushort color, byte alpha)
        {
            byte r = (byte)(((color >> 11) & 0x1F) * 255 / 31);
            byte g = (byte)(((color >> 5) & 0x3F) * 255 / 63);
            byte b = (byte)((color & 0x1F) * 255 / 31);
            return (uint)(r | (g << 8) | (b << 16) | (alpha << 24));
        }

        private static uint LerpRgb565(ushort c0, ushort c1, int w0, int w1, byte alpha)
        {
            int r0 = (c0 >> 11) & 0x1F;
            int g0 = (c0 >> 5) & 0x3F;
            int b0 = c0 & 0x1F;
            int r1 = (c1 >> 11) & 0x1F;
            int g1 = (c1 >> 5) & 0x3F;
            int b1 = c1 & 0x1F;
            int denom = w0 + w1;

            byte r = (byte)((r0 * w0 + r1 * w1) * 255 / (31 * denom));
            byte g = (byte)((g0 * w0 + g1 * w1) * 255 / (63 * denom));
            byte b = (byte)((b0 * w0 + b1 * w1) * 255 / (31 * denom));
            return (uint)(r | (g << 8) | (b << 16) | (alpha << 24));
        }
    }
}
