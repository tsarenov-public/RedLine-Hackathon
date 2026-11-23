using UnityEngine;
using UnityEngine.Rendering;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(Camera), typeof(NFSCameraController))]
    [DefaultExecutionOrder(70)]
    public sealed class CameraSpeedEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem speedLines;
        [SerializeField] private Volume speedVolume;
        [Range(25f, 100f)] public float baseFov = 60f;
        [Range(25f, 100f)] public float highSpeedFov = 72f;
        [Range(0f, 15f)] public float nitroFovKick = 7f;
        [Min(.01f)] public float response = .18f;
        [Min(0f)] public float lineStartKmh = 118f;
        [Min(0f)] public float maximumLineRate = 48f;

        private Camera view;
        private NFSCameraController follow;
        private float fovVelocity, weightVelocity;

        private void Awake()
        {
            view = GetComponent<Camera>();
            follow = GetComponent<NFSCameraController>();
            view.fieldOfView = baseFov;
        }

        private void OnEnable()
        {
            fovVelocity = weightVelocity = 0f;
            ApplyLines(0f);
            if (speedVolume != null) speedVolume.weight = 0f;
            if (speedLines != null) speedLines.Play();
        }

        private void OnDisable()
        {
            if (view != null) view.fieldOfView = baseFov;
            if (speedVolume != null) speedVolume.weight = 0f;
            if (speedLines != null) speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            var target = follow.isActiveAndEnabled ? follow.Target : null;
            float speed = target != null ? target.State.speedKmh : 0f;
            float nos = target != null ? Mathf.Clamp01(target.State.nosActive) : 0f;
            float speedFov = Mathf.InverseLerp(100f, 150f, speed);
            float goal = Mathf.Lerp(baseFov, highSpeedFov, speedFov) + nitroFovKick * nos;
            view.fieldOfView = Mathf.SmoothDamp(view.fieldOfView, goal, ref fovVelocity, response, Mathf.Infinity, dt);

            float wind = Mathf.InverseLerp(lineStartKmh, 255f, speed);
            ApplyLines(maximumLineRate * wind * (1f + nos * .6f));
            if (speedVolume != null)
            {
                float weight = Mathf.Clamp01(Mathf.InverseLerp(108f, 260f, speed) * .72f + nos * .5f);
                speedVolume.weight = Mathf.SmoothDamp(speedVolume.weight, weight, ref weightVelocity,
                    response, Mathf.Infinity, dt);
            }
        }

        private void ApplyLines(float rate)
        {
            if (speedLines == null) return;
            var emission = speedLines.emission;
            emission.rateOverTime = rate;
            if (view == null) return;
            float depth = Mathf.Abs(speedLines.transform.localPosition.z);
            float halfHeight = depth * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            var shape = speedLines.shape;
            shape.scale = new Vector3(halfHeight * view.aspect * .93f, halfHeight * .91f, 1f);
        }
    }
}
