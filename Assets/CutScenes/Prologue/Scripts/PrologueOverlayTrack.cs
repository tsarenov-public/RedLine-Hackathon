using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace RedLine.CutScenes
{
    [TrackColor(0.73f, 0.51f, 0.25f)]
    [TrackClipType(typeof(PrologueCue))]
    [TrackBindingType(typeof(PrologueOverlay))]
    public sealed class PrologueOverlayTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject owner, int inputCount)
        {
            return ScriptPlayable<OverlayMixer>.Create(graph, inputCount);
        }

        private sealed class OverlayMixer : PlayableBehaviour
        {
            public override void ProcessFrame(Playable playable, FrameData info, object playerData)
            {
                if (!(playerData is PrologueOverlay overlay)) return;
                for (int i = 0; i < playable.GetInputCount(); i++)
                {
                    if (playable.GetInputWeight(i) <= 0f) continue;
                    var input = (ScriptPlayable<PrologueCue.CueBehaviour>)playable.GetInput(i);
                    var cue = input.GetBehaviour();
                    float fade = cue.FadeSeconds > 0f
                        ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)input.GetTime() / cue.FadeSeconds))
                        : 0f;
                    overlay.Show(cue.Speaker, cue.Dialogue, fade);
                    return;
                }
                overlay.Show(string.Empty, string.Empty, 0f);
            }
        }
    }
}
