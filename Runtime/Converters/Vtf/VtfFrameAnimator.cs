using UnityEngine;

namespace Source2Unity.Converters.Vtf
{
    /// <summary>
    /// Cycles through VTF animation frames on a renderer material at runtime.
    /// Attach after loading an animated VTF or assign frames from import sub-assets.
    /// </summary>
    public sealed class VtfFrameAnimator : MonoBehaviour
    {
        [Tooltip("Animation frames (frame 0..N-1).")]
        public Texture2D[] Frames;

        [Tooltip("Material texture property to animate.")]
        public string TextureProperty = "_BaseMap";

        [Tooltip("Frames per second.")]
        public float FrameRate = 15f;

        [Tooltip("Also set Standard shader _MainTex when animating URP _BaseMap.")]
        public bool MirrorMainTex = true;

        private Renderer _renderer;
        private Material _materialInstance;
        private int _frameIndex;
        private float _timer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
                _materialInstance = _renderer.material;
        }

        private void Update()
        {
            if (Frames == null || Frames.Length <= 1 || _materialInstance == null)
                return;

            _timer += Time.deltaTime;
            float frameDuration = FrameRate > 0f ? 1f / FrameRate : 0.1f;
            if (_timer < frameDuration)
                return;

            _timer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % Frames.Length;
            ApplyFrame(Frames[_frameIndex]);
        }

        public void SetFrames(Texture2D[] frames, float frameRate = 15f)
        {
            Frames = frames;
            FrameRate = frameRate;
            _frameIndex = 0;
            _timer = 0f;

            if (Frames != null && Frames.Length > 0)
                ApplyFrame(Frames[0]);
        }

        private void ApplyFrame(Texture2D frame)
        {
            if (frame == null || _materialInstance == null)
                return;

            if (_materialInstance.HasProperty(TextureProperty))
                _materialInstance.SetTexture(TextureProperty, frame);

            if (MirrorMainTex && TextureProperty == "_BaseMap" && _materialInstance.HasProperty("_MainTex"))
                _materialInstance.SetTexture("_MainTex", frame);
        }

        private void OnDestroy()
        {
            if (_materialInstance != null)
                Destroy(_materialInstance);
        }
    }
}
