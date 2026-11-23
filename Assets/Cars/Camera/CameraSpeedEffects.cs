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
        [Range(.5f, 2.5f), Tooltip("Scales the authored width of newly emitted speed lines.")]
        public float speedLinesSizeMultiplier = 1f;
        [Range(0f, 2.5f), Tooltip("Scales the speed line emission rate, including under NOS.")]
        public float speedLinesIntensity = 1.2f;
        [Range(0f, 2f), Tooltip("Adjusts the opacity and brightness of the live streaks.")]
        public float speedLinesBrightness = 1.15f;

        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private Camera view;
        private NFSCameraController follow;
        private ParticleSystemRenderer lineRenderer;
        private MaterialPropertyBlock lineProperties;
        private Vector2 authoredLineSize;
        private float appliedSize = -1f, appliedBrightness = -1f;
        private float fovVelocity, weightVelocity;
        private float pendingLines;

        private void Awake()
        {
            view = GetComponent<Camera>();
            follow = GetComponent<NFSCameraController>();
            view.fieldOfView = baseFov;
            if (speedLines != null)
            {
                var size = speedLines.main.startSize;
                authoredLineSize = new Vector2(size.constantMin, size.constantMax);
                lineRenderer = speedLines.GetComponent<ParticleSystemRenderer>();
                lineProperties = new MaterialPropertyBlock();
            }
        }

        private void OnEnable()
        {
            fovVelocity = weightVelocity = 0f;
            pendingLines = 0f;
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
            ApplyLines(maximumLineRate * speedLinesIntensity * wind * (1f + nos * .6f), Time.deltaTime);
            if (speedVolume != null)
            {
                float weight = Mathf.Clamp01(Mathf.InverseLerp(108f, 260f, speed) * .72f + nos * .5f);
                speedVolume.weight = Mathf.SmoothDamp(speedVolume.weight, weight, ref weightVelocity,
                    response, Mathf.Infinity, dt);
            }
        }

        private void ApplyLines(float rate, float simulationDelta = 0f)
        {
            if (speedLines == null) return;
            var emission = speedLines.emission;
            emission.enabled = false;
            emission.rateOverTime = rate;
            if (!Mathf.Approximately(appliedSize, speedLinesSizeMultiplier))
            {
                appliedSize = speedLinesSizeMultiplier;
                var main = speedLines.main;
                main.startSize = new ParticleSystem.MinMaxCurve(authoredLineSize.x * appliedSize, authoredLineSize.y * appliedSize);
            }
            if (lineRenderer != null && !Mathf.Approximately(appliedBrightness, speedLinesBrightness))
            {
                appliedBrightness = speedLinesBrightness;
                lineRenderer.GetPropertyBlock(lineProperties);
                lineProperties.SetFloat(BrightnessId, appliedBrightness);
                lineRenderer.SetPropertyBlock(lineProperties);
            }
            if (view == null) return;
            float depth = Mathf.Abs(speedLines.transform.localPosition.z);
            float halfHeight = depth * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            var shape = speedLines.shape;
            shape.scale = new Vector3(halfHeight * view.aspect * .98f, halfHeight * .93f, .25f);

            // Let the donut choose positions, but send each streak straight toward the camera.
            if (rate <= 0f) { pendingLines = 0f; return; }
            pendingLines = Mathf.Min(16f, pendingLines + rate * simulationDelta);
            int count = (int)pendingLines;
            pendingLines -= count;
            var speed = speedLines.main.startSpeed;
            while (count-- > 0)
            {
                var particle = new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    applyShapeToPosition = true,
                    velocity = Vector3.forward * Random.Range(speed.constantMin, speed.constantMax)
                };
                speedLines.Emit(particle, 1);
            }
        }
    }
}
