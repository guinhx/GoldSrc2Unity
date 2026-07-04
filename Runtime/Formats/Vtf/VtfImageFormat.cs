namespace Source2Unity.Formats.Vtf
{
    /// <summary>
    /// Valve IMAGE_FORMAT enum (Source SDK). Values match on-disk VTF format field.
    /// </summary>
    public enum VtfImageFormat
    {
        Rgba8888 = 0,
        Abgr8888 = 1,
        Rgb888 = 2,
        Bgr888 = 3,
        Rgb565 = 4,
        I8 = 5,
        Ia88 = 6,
        P8 = 7,
        A8 = 8,
        Rgb888Bluescreen = 9,
        Bgr888Bluescreen = 10,
        Argb8888 = 11,
        Bgra8888 = 12,
        Dxt1 = 13,
        Dxt3 = 14,
        Dxt5 = 15,
        Bgrx8888 = 16,
        Bgr565 = 17,
        Bgrx5551 = 18,
        Bgra4444 = 19,
        Dxt1OneBitAlpha = 20,
        Bgra5551 = 21,
        Unknown = -1
    }
}
