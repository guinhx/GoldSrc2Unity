using UnityEngine;

namespace Source2Unity.Converters
{
    /// <summary>
    /// GoldSrc (X-forward, Y-left, Z-up, Right-Handed) to Unity (X-right, Y-up, Z-forward, Left-Handed)
    /// coordinate conversion utilities. Reusable across MDL, BSP, WAD, or any GoldSrc format.
    /// </summary>
    public static class GoldSrcCoordinates
    {
        /// <summary>1 GoldSrc unit = 1 inch = 0.0254 meters.</summary>
        public const float Scale = 0.0254f;

        /// <summary>
        /// Converts a GoldSrc position to Unity coordinates with scale.
        /// Mapping: Unity.X = -GS.Y, Unity.Y = GS.Z, Unity.Z = GS.X
        /// </summary>
        public static Vector3 PositionToUnity(float gx, float gy, float gz)
        {
            return new Vector3(-gy * Scale, gz * Scale, gx * Scale);
        }

        /// <summary>
        /// Converts GoldSrc Euler angles (radians) to a Unity quaternion.
        /// Matches Valve's AngleQuaternion (r_studio.cpp) exactly, then remaps
        /// axes GS(X,Y,Z) to Unity(-Y,Z,X) with RH-to-LH conjugation.
        /// </summary>
        public static Quaternion RotationToUnity(float v3, float v4, float v5)
        {
            float hp = v3 * 0.5f, hy = v4 * 0.5f, hr = v5 * 0.5f;
            float sp = Mathf.Sin(hp), cp = Mathf.Cos(hp);
            float sy = Mathf.Sin(hy), cy = Mathf.Cos(hy);
            float sr = Mathf.Sin(hr), cr = Mathf.Cos(hr);

            float gx = sp * cy * cr - cp * sy * sr;
            float gy = cp * sy * cr + sp * cy * sr;
            float gz = cp * cy * sr - sp * sy * cr;
            float gw = cp * cy * cr + sp * sy * sr;

            return new Quaternion(gy, -gz, -gx, gw);
        }
    }
}
