using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(Camera), typeof(NFSCameraController))]
    [DefaultExecutionOrder(70)]
    public sealed class CameraSpeedEffects : MonoBehaviour
    {
        [SerializeField] private Volume speedVolume;
        [Range(25f, 100f)] public float baseFov = 60f;
        [Range(25f, 100f)] public float highSpeedFov = 72f;
        [Range(0f, 15f)] public float nitroFovKick = 7f;
        [Min(.01f)] public float response = .18f;
        [Min(0f), Tooltip("Motion blur is disabled at or below this speed in km/h.")]
        public float motionBlurStartKmh = 100f;
        [Range(0f, 1f), Tooltip("Maximum motion blur intensity without nitro, reached at 260 km/h.")]
        public float maxMotionBlurIntensity = .75f;
        [Range(0f, 1f), Tooltip("Target intensity while nitro is active above the speed threshold.")]
        public float nitroMotionBlurIntensity = .95f;

        private Camera view;
        private NFSCameraController follow;
        private MotionBlur motionBlur;
        private Vignette vignette;
        private ChromaticAberration aberration;
        private LensDistortion distortion;
        private float vignettePeak, aberrationPeak, distortionPeak;
        private float fovVelocity, blurVelocity, edgeVelocity, edgeAccent;

        private void Awake()
        {
            view = GetComponent<Camera>();
            follow = GetComponent<NFSCameraController>();
            if (speedVolume == null || speedVolume.sharedProfile == null) return;

            // Volume.profile creates a private copy so play-mode tuning leaves the asset intact.
            var profile = speedVolume.profile;
            profile.TryGet(out motionBlur);
            if (profile.TryGet(out vignette)) vignettePeak = vignette.intensity.value;
            if (profile.TryGet(out aberration)) aberrationPeak = aberration.intensity.value;
            if (profile.TryGet(out distortion)) distortionPeak = distortion.intensity.value;
        }

        private void OnEnable() => ResetEffects();

        private void OnDisable() => ResetEffects();

        private void ResetEffects()
        {
            fovVelocity = blurVelocity = edgeVelocity = edgeAccent = 0f;
            if (view != null) view.fieldOfView = baseFov;
            if (motionBlur != null) motionBlur.intensity.value = 0f;
            if (speedVolume != null) speedVolume.weight = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            var target = follow.isActiveAndEnabled ? follow.Target : null;
            float speed = target != null ? target.State.speedKmh : 0f;
            float nos = target != null ? Mathf.Clamp01(target.State.nosActive) : 0f;
            float speedFov = Mathf.InverseLerp(100f, 150f, speed);
            float fov = Mathf.Lerp(baseFov, highSpeedFov, speedFov) + nitroFovKick * nos;
            view.fieldOfView = Mathf.SmoothDamp(view.fieldOfView, fov, ref fovVelocity, response, Mathf.Infinity, dt);

            if (speedVolume == null) return;

            float ramp = Mathf.InverseLerp(motionBlurStartKmh, Mathf.Max(260f, motionBlurStartKmh + 1f), speed);
            float edgeGoal = speed > motionBlurStartKmh ? Mathf.Lerp(ramp * .55f, 1f, nos) : 0f;
            edgeAccent = Mathf.SmoothDamp(edgeAccent, edgeGoal, ref edgeVelocity, response, Mathf.Infinity, dt);
            if (motionBlur != null)
            {
                if (speed <= motionBlurStartKmh)
                {
                    motionBlur.intensity.value = 0f;
                    blurVelocity = 0f;
                }
                else
                {
                    float blurGoal = Mathf.Lerp(ramp * maxMotionBlurIntensity,
                        Mathf.Max(maxMotionBlurIntensity, nitroMotionBlurIntensity), nos);
                    motionBlur.intensity.value = Mathf.SmoothDamp(motionBlur.intensity.value, blurGoal, ref blurVelocity,
                        response, Mathf.Infinity, dt);
                }
            }

            if (vignette != null) vignette.intensity.value = vignettePeak * edgeAccent;
            if (aberration != null) aberration.intensity.value = aberrationPeak * edgeAccent;
            if (distortion != null) distortion.intensity.value = distortionPeak * edgeAccent;
            speedVolume.weight = edgeAccent > .001f || (motionBlur != null && motionBlur.IsActive()) ? 1f : 0f;
        }
    }
}
