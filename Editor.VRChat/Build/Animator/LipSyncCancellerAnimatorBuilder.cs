using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>発話中のLipSync共通補正を適用するglobal layerを構築する。</summary>
internal sealed class LipSyncCancellerAnimatorBuilder
{
    private static readonly Vector3 LayoutOrigin = new(300, 0, 0);
    private const float TransitionDurationSeconds = 0.05f;
    private const string VoiceParameterName = "Voice";
    private const float VoiceThreshold = 0.01f;

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly MmdSupport _mmdSupport;
    private readonly VRChatTrackingPlan _plan;
    private readonly AapProtocol _aap;

    public LipSyncCancellerAnimatorBuilder(
        AvatarContext avatarContext,
        AnimatorGraph graph,
        MmdSupport mmdSupport,
        VRChatTrackingPlan plan,
        AapProtocol aap)
    {
        _avatarContext = avatarContext;
        _graph = graph;
        _mmdSupport = mmdSupport;
        _plan = plan;
        _aap = aap;
    }

    public void Build(VirtualAnimatorController controller, int layerPriority)
    {
        var cancellers = _plan.GeneratedLipSyncSettings
            .Select((settings, index) => (
                Settings: settings,
                Mode: VRChatTrackingPlan.FirstCustomMode + index))
            .Where(entry => entry.Settings.CancellerBlendShapes.Count > 0)
            .ToImmutableList();
        if (cancellers.Count == 0) return;

        var modeConditions = cancellers
            .Select(entry => _aap.LipSyncModeIs(entry.Mode))
            .ToImmutableList();
        EnsureParameters(controller, modeConditions);

        var layer = _graph.AddLayer(controller, "Lip Sync Canceller", layerPriority);
        var root = layer.StateMachine!;
        var xStep = AnimatorGraph.PositionXStep;
        var yStep = AnimatorGraph.PositionYStep;
        var evaluation = _graph.AddState(layer, "Mode Evaluation", LayoutOrigin);
        _graph.AsPassThrough(evaluation);

        var initial = _graph.AddInitialDelayState(
            layer,
            LayoutOrigin + new Vector3(0, yStep * 2, 0));
        _graph.AddExitTimeTransition(initial, evaluation);

        _mmdSupport.AddPassThroughState(
            layer,
            LayoutOrigin - new Vector3(0, yStep * 2, 0),
            evaluation);

        var voiceActiveWhen = VoiceActiveWhen();
        var position = LayoutOrigin + new Vector3(xStep, -yStep, 0);
        for (var index = 0; index < cancellers.Count; index++)
        {
            var entry = cancellers[index];
            var modeWhen = modeConditions[index];
            var machine = _graph.AddStateMachine(
                root,
                $"Canceller {index + 1}",
                position);
            var idle = _graph.AddState(machine, "Idle", LayoutOrigin);
            var cancelling = _graph.AddState(
                machine,
                "Cancelling",
                LayoutOrigin + new Vector3(0, yStep, 0));
            _graph.AsPassThrough(idle);
            SetCancellerClip(cancelling, entry.Settings);

            _graph.AddStateTransition(evaluation, machine, modeWhen, 0f);
            _graph.AddEntryTransition(machine, idle);
            _graph.AddExitTransitions(idle, modeWhen.Complement(), 0f);
            _graph.AddExitTransitions(cancelling, modeWhen.Complement(), 0f);
            _graph.AddStateTransition(
                idle,
                cancelling,
                voiceActiveWhen,
                TransitionDurationSeconds);
            _graph.AddStateTransition(
                cancelling,
                idle,
                voiceActiveWhen.Complement(),
                TransitionDurationSeconds);
            _graph.AddStateMachineTransition(root, machine, evaluation);

            position.y += yStep;
        }
    }

    private void EnsureParameters(
        VirtualAnimatorController controller,
        ImmutableList<DnfCondition> modeConditions)
    {
        _aap.EnsureLipSyncParameters(controller);
        controller.EnsureFloatParameterExists(VoiceParameterName);
        AnimatorGraph.EnsureConditionParameters(
            controller,
            modeConditions
                .Append(_mmdSupport.LayerPlaybackWhen)
                .ToArray());
    }

    private void SetCancellerClip(VirtualState state, LipSyncSettings settings)
    {
        state.SetNewClip(state.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            new BlendShapeWeightSet(settings.CancellerBlendShapes)
                .ToBlendShapeAnimations());
    }

    private static DnfCondition VoiceActiveWhen()
    {
        var condition = ParameterCondition.Float(
            VoiceParameterName,
            ComparisonType.GreaterThan,
            VoiceThreshold);
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            ParameterDomainRegistry.Empty);
    }
}
