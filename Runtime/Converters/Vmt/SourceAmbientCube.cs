using UnityEngine;

namespace Source2Unity.Converters.Vmt
{
    /// <summary>
    /// Configures the six-direction ambient cube used by Source VertexLitGeneric (SIGGRAPH 2006 Listing 1).
    /// </summary>
    public static class SourceAmbientCube
    {
        public const string Keyword = "_AMBIENT_CUBE";

        public static void ApplyDefault(Material material)
        {
            if (material == null)
                return;

            material.SetColor("_AmbientCubePY", RenderSettings.ambientSkyColor.linear);
            material.SetColor("_AmbientCubeNY", RenderSettings.ambientGroundColor.linear);
            Color equator = RenderSettings.ambientEquatorColor.linear;
            material.SetColor("_AmbientCubePX", equator);
            material.SetColor("_AmbientCubeNX", equator);
            material.SetColor("_AmbientCubePZ", equator);
            material.SetColor("_AmbientCubeNZ", equator);
            material.EnableKeyword(Keyword);
        }

        public static void ApplyCustom(
            Material material,
            Color positiveX,
            Color negativeX,
            Color positiveY,
            Color negativeY,
            Color positiveZ,
            Color negativeZ)
        {
            if (material == null)
                return;

            material.SetColor("_AmbientCubePX", positiveX.linear);
            material.SetColor("_AmbientCubeNX", negativeX.linear);
            material.SetColor("_AmbientCubePY", positiveY.linear);
            material.SetColor("_AmbientCubeNY", negativeY.linear);
            material.SetColor("_AmbientCubePZ", positiveZ.linear);
            material.SetColor("_AmbientCubeNZ", negativeZ.linear);
            material.EnableKeyword(Keyword);
        }
    }
}
