using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class AfkSupport
{
    public DnfCondition PlaybackWhen { get; }

    public AfkSupport(bool enabled)
    {
        PlaybackWhen = enabled
            ? DnfCondition.Single(new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = "AFK",
                    mode = AnimatorConditionMode.If
                },
                AnimatorControllerParameterType.Bool),
            ParameterDomainRegistry.Empty)
            : DnfCondition.Never;
    }

    public void AddPlaybackState(
        VirtualAnimatorController controller,
        AnimatorGraph graph,
        VirtualLayer layer,
        VirtualState defaultState,
        Vector3 position)
    {
        if (PlaybackWhen.IsNever) return;
        AnimatorGraph.EnsureConditionParameters(controller, PlaybackWhen);

        var playback = graph.AddState(layer, "AFK Playback", position);
        playback.SetNewClip("AFK Playback")
            .SetAap(AapProtocol.ExpressionInactiveName, 1f);
        graph.AddEntryTransition(layer, playback, PlaybackWhen);
        graph.AddExitTransitions(defaultState, PlaybackWhen, 0f);
        graph.SetExitTransitions(playback, PlaybackWhen.Complement(), 0f);
    }
}
