using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace RedLine.CutScenes
{
    public sealed class PrologueCue : PlayableAsset, ITimelineClipAsset
    {
        public string speaker;
        [TextArea] public string dialogue;
        [Min(0f)] public float fadeSeconds;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CueBehaviour>.Create(graph);
            var cue = playable.GetBehaviour();
            cue.Speaker = speaker;
            cue.Dialogue = dialogue;
            cue.FadeSeconds = fadeSeconds;
            return playable;
        }

        internal sealed class CueBehaviour : PlayableBehaviour
        {
            internal string Speaker;
            internal string Dialogue;
            internal float FadeSeconds;
        }
    }
}
