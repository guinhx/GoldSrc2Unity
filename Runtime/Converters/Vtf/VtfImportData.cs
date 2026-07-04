using UnityEngine;

namespace Source2Unity.Converters.Vtf
{
    /// <summary>
    /// Imported VTF metadata stored as a sub-asset for animated textures, cubemaps, and volumes.
    /// </summary>
    public sealed class VtfImportData : ScriptableObject
    {
        public bool IsCubemap;
        public bool IsAnimated;
        public bool IsVolume;
        public float FrameRate = 15f;
        public Texture2D[] AnimationFrames;
        public Cubemap Cubemap;
        public Texture3D VolumeTexture;
        public Texture2D SpriteSheet;
    }
}
