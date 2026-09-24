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

    public void AddInitialState(
        VirtualAnimatorController controller,
        AnimatorGraph graph,
        VirtualLayer layer,
        VirtualState defaultState,
        IReadOnlyList<BlendShapeWeight> blendShapes,
        string bodyPath,
        Vector3 position)
    {
        if (PlaybackWhen.IsNever) return;
        AnimatorGraph.EnsureConditionParameters(controller, PlaybackWhen);
        var root = layer.StateMachine!;
        var machine = graph.AddStateMachine(root, "AFK", position);
        var initial = graph.AddState(machine, "Initial", new Vector3(300, 0, 0));
        var initialClip = initial.SetNewClip("AFK Initial");
        initialClip.AddBlendShapeAnimations(bodyPath, blendShapes.ToBlendShapeAnimations());
        initialClip.SetAap(AapProtocol.ExpressionInactiveName, 1f, 1f);
        machine.DefaultState = initial;

        var playback = graph.AddState(machine, "Playback", new Vector3(550, 0, 0));
        playback.SetNewClip("AFK Playback")
            .SetAap(AapProtocol.ExpressionInactiveName, 1f);

        graph.AddExitTransitions(initial, PlaybackWhen.Complement(), 0f);
        graph.AddExitTimeTransition(initial, playback);
        graph.AddExitTransitions(playback, PlaybackWhen.Complement(), 0f);
        graph.AddEntryTransition(layer, machine, PlaybackWhen);
        graph.AddStateMachineExitTransition(root, machine);
        graph.AddExitTransitions(defaultState, PlaybackWhen, 0f);
    }
}
