using System.Collections.Generic;

namespace Source2Unity.Converters.Pipeline
{
    /// <summary>
    /// Maps common Source shader keys to Unity shader property names.
    /// Does not replicate Source shaders — targets URP Lit/Unlit with Standard fallback.
    /// </summary>
    internal static class SourceShaderMapping
    {
        public static readonly IReadOnlyDictionary<string, string> BaseTextureKeys = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["$basetexture"] = "_BaseMap",
            ["$basetexture2"] = "_BaseMap",
            ["$texture"] = "_BaseMap",
            ["$detail"] = "_DetailAlbedoMap",
        };

        public static readonly IReadOnlyDictionary<string, string> BumpMapKeys = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["$bumpmap"] = "_BumpMap",
            ["$normalmap"] = "_BumpMap",
            ["$bumpmap2"] = "_BumpMap",
        };

        public static readonly IReadOnlyDictionary<string, string> SelfIllumKeys = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["$selfillum"] = "_EmissionMap",
            ["$selfillummask"] = "_EmissionMap",
        };

        public static readonly IReadOnlyDictionary<string, string> PhongExponentKeys = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["$phongexponenttexture"] = "_PhongExponentMap",
        };

        public static readonly string[] LightMapKeys =
        {
            "$lightmap",
            "$lightmaptexture",
        };

        public static readonly string[] RadiosityLightMapKeys =
        {
            "$lightmap0",
            "$lightmap1",
            "$lightmap2",
        };

        public static readonly string[] VolumeTextureKeys =
        {
            "$volumetexture",
            "$volumeTexture",
            "$cloudtexture",
        };

        public static readonly IReadOnlyDictionary<string, string> StandardFallbackProperties = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["_BaseMap"] = "_MainTex",
            ["_BumpMap"] = "_BumpMap",
            ["_EmissionMap"] = "_EmissionMap",
            ["_DetailAlbedoMap"] = "_DetailAlbedoMap",
        };

        public static readonly string[] UnlitShaderNames =
        {
            "UnlitGeneric",
            "UnlitTwoTexture",
            "Sprite",
            "Wireframe",
        };

        public static readonly string[] DecalShaderNames =
        {
            "DecalModulate",
            "LightmappedDecal",
        };

        public static readonly string[] EnvCubemapProperties =
        {
            "_SpecCubemap",
            "_Cube",
            "_Cubemap",
            "_EnvCube",
            "_EnvironmentCube",
            "_ReflectionCube",
        };
    }
}
