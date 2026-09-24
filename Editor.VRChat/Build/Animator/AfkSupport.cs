using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class AfkSupport
{
    public DnfCondition PlaybackWhen { get; }
    public bool DisableFxLayer { get; }

    public AfkSupport(AfkPlaybackSettings settings)
    {
        PlaybackWhen = settings.Enabled
            ? DnfCondition.Single(new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = "AFK",
                    mode = AnimatorConditionMode.If
                },
                AnimatorControllerParameterType.Bool),
            ParameterDomainRegistry.Empty)
            : DnfCondition.Never;
        if (PlaybackWhen.IsNever) return;

        DisableFxLayer = settings.DisableMode switch
        {
            AFKSupportSettings.Mode.DisableFaceTune => false,
            AFKSupportSettings.Mode.DisableFXlayer => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(settings.DisableMode), settings.DisableMode, null)
        };
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
        graph.AddEntryTransition(layer, playback, PlaybackWhen);
        graph.AddExitTransitions(defaultState, PlaybackWhen, 0f);

        if (!DisableFxLayer)
        {
            playback.SetNewClip("AFK Playback")
                .SetAap(AapProtocol.ExpressionInactiveName, 1f);
            graph.SetExitTransitions(playback, PlaybackWhen.Complement(), 0f);
            return;
        }

        graph.AsPassThrough(playback);
        playback.SetFxPlayableWeight(0f);

        var restore = graph.AddState(
            layer,
            "Restore FX",
            position + new Vector3(AnimatorGraph.PositionXStep, 0, 0));
        graph.AsPassThrough(restore);
        restore.SetFxPlayableWeight(1f);
        graph.AddStateTransition(playback, restore, PlaybackWhen.Complement(), 0f);
        graph.AddExitTransitions(restore, DnfCondition.Always, 0f);
    }
}
