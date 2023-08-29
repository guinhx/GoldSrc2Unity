using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GoldSrc2Unity.Source.IO;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MdlTextureEntry
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;

    public int Flags;
    public int Width;
    public int Height;
    public int Index;

    public bool IsChrome => (Flags & 2) > 0;
    public bool IsTransparent => (Flags & 64) > 0;
    public bool IsAdditive => (Flags & 32) > 0;

    public List<byte> GetTextureData(BinaryReader reader)
    {
        reader.BaseStream.Seek(Index, SeekOrigin.Begin);
        var result = new List<byte>();
        // TODO: Check if has errors: https://github.com/DataPlusProgram/Godot-GoldSrc-MDL-Importer/blob/5935d2541962ec3556b42ec0ff596a477d6cce2f/addons/GoldSRC_mdl_importer/mdlLoad.gd#L268
        var colors = new List<byte>();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var color = reader.ReadByte();
                colors.Add(color);
            }
        }

        var palette = new List<Color>();
        for (var i = 0; i < 256; i++)
        {
            var r = reader.ReadByte() / 255f;
            var g = reader.ReadByte() / 255f;
            var b = reader.ReadByte() / 255f;
            palette.Add(new Color(r, g, b));
        }

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var color = colors[y * Width + x];
                var paletteColor = palette[color];
                result.Add((byte)(paletteColor.r * 255));
                result.Add((byte)(paletteColor.g * 255));
                result.Add((byte)(paletteColor.b * 255));
            }
        }

        return result;
    }
}