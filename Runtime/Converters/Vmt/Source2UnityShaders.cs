using System.Globalization;
using Source2Unity.Formats.KeyValues;
using Source2Unity.Formats.Mdl.Structures;
using UnityEngine;
using UnityEngine.Rendering;

namespace Source2Unity.Converters.Vmt
{
    /// <summary>
    /// Resolves and configures Source2Unity URP shaders for imported VMT materials.
    /// Based on Source Engine VertexLitGeneric behavior (SIGGRAPH 2006).
    /// </summary>
    public static class Source2UnityShaders
    {
        public const string Standard = "Source2Unity/Source Standard";
        public const string Lightmapped = "Source2Unity/Source Lightmapped";
        public const string Unlit = "Source2Unity/Source Unlit";
        public const string Sky = "Source2Unity/Source Sky";
        public const string GoldSrc = "Source2Unity/Source GoldSrc";

        public static Shader FindStandard()
        {
            return Shader.Find(Standard)
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
        }

        public static Shader FindLightmapped()
        {
            return Shader.Find(Lightmapped)
                ?? FindStandard();
        }

        public static Shader FindUnlit()
        {
            return Shader.Find(Unlit)
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? FindStandard();
        }

        public static Shader FindSky()
        {
            return Shader.Find(Sky)
                ?? Shader.Find("Skybox/Cubemap")
                ?? FindUnlit();
        }

        public static Shader FindGoldSrc()
        {
            return Shader.Find(GoldSrc)
                ?? FindUnlit();
        }

        /// <summary>Applies blend/cull/render-queue state only — does not touch feature keywords.</summary>
        public static void ApplySurfaceState(Material material, KvObject root)
        {
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

            bool alphatest = IsTruthy(root.GetString("$alphatest"));
            bool translucent = IsTruthy(root.GetString("$translucent")) || IsTruthy(root.GetString("$alpha"));
            bool additive = IsTruthy(root.GetString("$additive"));

            if (alphatest)
                material.EnableKeyword("_ALPHATEST_ON");

            if (translucent || additive)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_ZWrite", 0f);

                if (additive)
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.One);
                }
                else
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                }

                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_ZWrite", 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.renderQueue = alphatest ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            }

            if (IsTruthy(root.GetString("$nocull")))
                material.SetFloat("_Cull", (float)CullMode.Off);
            else
                material.SetFloat("_Cull", (float)CullMode.Back);
        }

        public static void ApplySourceLightingKeywords(Material material, KvObject root)
        {
            if (IsTruthy(root.GetString("$halflambert")) || IsTruthy(root.GetString("$model")))
                material.EnableKeyword("_HALFLAMBERT");

            if (IsTruthy(root.GetString("$phong")))
                material.EnableKeyword("_PHONG");

            if (IsTruthy(root.GetString("$rimlight")))
                material.EnableKeyword("_RIMLIGHT");

            if (IsTruthy(root.GetString("$selfillum")))
            {
                material.EnableKeyword("_SELFILLUM");
                if (string.IsNullOrEmpty(root.GetString("$selfillummask")))
                    material.EnableKeyword("_SELFILLUM_MASK_BASEALPHA");
            }

            if (IsTruthy(root.GetString("$basealphaenvmapmask")))
                material.EnableKeyword("_ENVMAPMASK_BASEALPHA");

            if (IsTruthy(root.GetString("$normalmapalphaenvmapmask")))
                material.EnableKeyword("_ENVMAPMASK_BUMPALPHA");
        }

        public static void ApplyAmbientCube(Material material)
        {
            SourceAmbientCube.ApplyDefault(material);
        }

        public static void ApplyPhongExponentTexture(Material material, Texture2D texture, KvObject root)
        {
            if (texture == null)
                return;

            material.SetTexture("_PhongExponentMap", texture);
            material.EnableKeyword("_PHONG_EXPONENTMAP");

            if (float.TryParse(root.GetString("$phongexponent"), NumberStyles.Float, CultureInfo.InvariantCulture, out float scale))
                material.SetFloat("_PhongExponentScale", Mathf.Max(scale, 1f));
            else
                material.SetFloat("_PhongExponentScale", 128f);
        }

        public static void ApplyLightmap(Material material, Texture2D lightMap, Texture2D rnm0, Texture2D rnm1, Texture2D rnm2)
        {
            if (rnm0 != null && rnm1 != null && rnm2 != null)
            {
                material.SetTexture("_LightMapBump0", rnm0);
                material.SetTexture("_LightMapBump1", rnm1);
                material.SetTexture("_LightMapBump2", rnm2);
                material.EnableKeyword("_RNM");
                material.EnableKeyword("_LIGHTMAP");
                return;
            }

            if (lightMap != null)
            {
                material.SetTexture("_LightMap", lightMap);
                material.EnableKeyword("_LIGHTMAP");
            }
        }

        public static void ApplySourceScalarProperties(Material material, KvObject root)
        {
            if (float.TryParse(root.GetString("$phongboost"), NumberStyles.Float, CultureInfo.InvariantCulture, out float phongBoost))
                material.SetFloat("_PhongBoost", phongBoost);

            if (float.TryParse(root.GetString("$phongexponent"), NumberStyles.Float, CultureInfo.InvariantCulture, out float phongExp))
                material.SetFloat("_PhongExponent", phongExp);

            if (TryParseVector3(root.GetString("$phongfresnelranges"), out Vector3 fresnelRanges))
                material.SetVector("_PhongFresnelRanges", fresnelRanges);

            if (float.TryParse(root.GetString("$rimlightboost"), NumberStyles.Float, CultureInfo.InvariantCulture, out float rimBoost))
                material.SetFloat("_RimBoost", rimBoost);

            if (float.TryParse(root.GetString("$rimlightexponent"), NumberStyles.Float, CultureInfo.InvariantCulture, out float rimExp))
                material.SetFloat("_RimExponent", rimExp);

            if (float.TryParse(root.GetString("$envmapfresnel"), NumberStyles.Float, CultureInfo.InvariantCulture, out float envFresnel))
                material.SetFloat("_EnvFresnelPower", Mathf.Clamp(envFresnel, 0.5f, 8f));
        }

        public static void EnableEnvmap(Material material, Cubemap cubemap)
        {
            if (cubemap == null)
                return;

            material.SetTexture("_EnvCubemap", cubemap);
            material.EnableKeyword("_ENVMAP");
        }

        public static void ConfigureGoldSrcMaterial(Material material, MdlTextureFlags flags)
        {
            material.EnableKeyword("_HALFLAMBERT");

            if ((flags & MdlTextureFlags.Additive) != 0)
            {
                material.EnableKeyword("_ADDITIVE_BLEND");
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.One);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else if ((flags & MdlTextureFlags.Masked) != 0 || (flags & MdlTextureFlags.Alpha) != 0)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cutoff", 0.5f);
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }

            if ((flags & MdlTextureFlags.Chrome) != 0)
                material.EnableKeyword("_CHROME");

            if ((flags & MdlTextureFlags.FlatShade) != 0)
                material.EnableKeyword("_FLATSHADE");

            if ((flags & MdlTextureFlags.FullBright) != 0)
                material.EnableKeyword("_FULLBRIGHT");
        }

        private static bool TryParseVector3(string value, out Vector3 result)
        {
            result = Vector3.zero;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim().Trim('[', ']', '{', '}');
            string[] parts = value.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return false;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out result.x))
                return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out result.y))
                return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out result.z))
                return false;

            return true;
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value != "0" && !value.Equals("false", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
