using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace RedLine.CutScenes
{
    public sealed class PrologueOverlay : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private CanvasGroup subtitles;
        [SerializeField] private Text speakerLabel;
        [SerializeField] private Text dialogueLabel;
        [SerializeField] private CanvasGroup blackout;
        [SerializeField] private AudioSource parkedMiata;
        private bool idleStarted;

        private void OnEnable()
        {
            if (!director) return;
            director.played += Begin;
            director.paused += Pause;
            director.stopped += Finish;
        }

        private void OnDisable()
        {
            if (director)
            {
                director.played -= Begin;
                director.paused -= Pause;
                director.stopped -= Finish;
            }
            StopIdle();
        }

        private void Start()
        {
            if (!director) return;
            director.Play();
            if (!idleStarted) StartIdle();
        }

        internal void Show(string speaker, string dialogue, float fade)
        {
            speakerLabel.text = speaker;
            dialogueLabel.text = dialogue;
            subtitles.alpha = string.IsNullOrEmpty(dialogue) ? 0f : 1f;
            blackout.alpha = fade;
        }

        private void Begin(PlayableDirector sequence)
        {
            if (!Application.isPlaying) return;
            blackout.alpha = 0f;
            if (!idleStarted || sequence.time <= 0.01) StartIdle();
            else if (parkedMiata) parkedMiata.UnPause();
        }

        private void Pause(PlayableDirector sequence)
        {
            if (Application.isPlaying && parkedMiata) parkedMiata.Pause();
        }

        private void Finish(PlayableDirector sequence)
        {
            if (!Application.isPlaying) return;
            StopIdle();
            Show(string.Empty, string.Empty, 1f);
        }

        private void StartIdle()
        {
            if (!parkedMiata) return;
            parkedMiata.Play();
            idleStarted = true;
        }

        private void StopIdle()
        {
            if (parkedMiata) parkedMiata.Stop();
            idleStarted = false;
        }
    }
}
