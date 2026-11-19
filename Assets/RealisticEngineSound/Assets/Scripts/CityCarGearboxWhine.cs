//______________________________________________//
//_________ Realistic Engine Sounds 2 __________//
//______________________________________________//
//______ Copyright © 2026 Skril Studio _________//
//______________________________________________//
//_________ https://skrilstudio.com/ ___________//
//______________________________________________//
//________ https://fb.com/yugelmobile/ _________//
//______________________________________________//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SkrilStudio
{
    public class CityCarGearboxWhine : MonoBehaviour // this script is used to simulate regular city cars gearbox whine, which can be heard at higher speeds - mostly heard in older cars but some affordable newer cars produce this noise too
    {
        RealisticEngineSound res2;
        private float clipsValue;
        // master volume setting
        [Range(0.1f, 1.0f)]
        public float masterVolume = 1f;
        // audio mixer
        public AudioMixerGroup audioMixer;
        // audio clips
        public AudioClip onLoopClip;
        // curve settings
        public AnimationCurve onLoadVolCurve;
        public AnimationCurve offLoadVolCurve;
        public AnimationCurve pitchCurve;
        // sound playing distance settings
        public float minDistance = 2;
        public float maxDistance = 70;
        // audio sources
        private AudioSource onLoop;
        private float _volume = 0;
        void Start()
        {
            res2 = gameObject.transform.parent.GetComponent<RealisticEngineSound>(); // find res2
            // use res2 audio mixer if there is no custom audio mixer
            if (audioMixer == null && res2.audioMixer != null)
                audioMixer = res2.audioMixer;
        }
        private void OnDisable() // destroy audio sources if disabled
        {
            DestroyAll();
        }
        private void OnEnable() // recreate all audio sources if script is reEnabled
        {
            StartCoroutine(WaitForStart());
        }
        private void DestroyAll()
        {
            if (onLoop != null)
                Destroy(onLoop);
        }
        void Update()
        {
            clipsValue = DynamicSoundController.BasicClipsValue(res2.carCurrentSpeed, res2.carMaxSpeed);

            if (res2.isAudible && res2.enabled)
            {
                // volume controller based on engine load
                _volume = Mathf.Lerp(offLoadVolCurve.Evaluate(clipsValue), onLoadVolCurve.Evaluate(clipsValue), res2.engineLoad) * masterVolume;
                // create on loop audio source if there is not yet any
                if (onLoop == null)
                {
                    CreateOnLoop();
                }
                else
                {
                    onLoop.volume = _volume;
                    onLoop.pitch = pitchCurve.Evaluate(clipsValue);
                    if (!onLoop.isPlaying)
                        onLoop.Play();
                } 
            }
            else
            {
                DestroyAll();
            }
        }
#if UNITY_EDITOR
        private void LateUpdate()
        {
            // velocity update mode got changed on runtime, remake all audio sources
            if (onLoop != null && onLoop.velocityUpdateMode != res2.audioVelocityUpdateMode)
                DestroyAll();
        }
#endif
        IEnumerator WaitForStart()
        {
            while (true)
            {
                yield return 0.1f; // this is needed to avoid duplicate audio sources
                    Start();
                break;
            }
        }
        // create audio sources
        void CreateOnLoop()
        {
            if (onLoopClip != null)
            {
                onLoop = gameObject.AddComponent<AudioSource>();
                onLoop.rolloffMode = res2.audioRolloffMode;
                onLoop.dopplerLevel = res2.dopplerLevel;
                onLoop.volume = _volume;
                onLoop.pitch = pitchCurve.Evaluate(clipsValue);
                onLoop.minDistance = minDistance;
                onLoop.maxDistance = maxDistance;
                onLoop.spatialBlend = res2.spatialBlend;
                onLoop.velocityUpdateMode = res2.audioVelocityUpdateMode;
                onLoop.loop = true;
                if (audioMixer != null)
                    onLoop.outputAudioMixerGroup = audioMixer;
                onLoop.clip = onLoopClip;
                onLoop.Play();
            }
        }
    }
}
