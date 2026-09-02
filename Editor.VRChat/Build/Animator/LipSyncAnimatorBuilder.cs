using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>FaceTuneのtracking設定から単一のglobal LipSync layerを構築する。</summary>
internal sealed class LipSyncAnimatorBuilder
{
    private static readonly Vector3 LayoutOrigin = new(300, 0, 0);
    private const float CancellerTransitionDurationSeconds = 0.05f;
    private const string VoiceParameterName = "Voice";
    private const float VoiceThreshold = 0.01f;

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly MmdSupport _mmdSupport;
    private readonly VRChatTrackingPlan _plan;
    private readonly AapProtocol _aap;

    public LipSyncAnimatorBuilder(
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

    public void Build(
        VirtualAnimatorController controller,
        int layerPriority)
    {
        var cancellers = _plan.LipSyncCancellers
            .Select((settings, index) => (
                Settings: settings,
                Mode: VRChatTrackingPlan.FirstCustomMode + index))
            .ToImmutableList();
        var cancellerConditions = cancellers
            .Select(entry => _aap.LipSyncModeIs(entry.Mode))
            .ToImmutableList();
        var disabledWhen = _aap.LipSyncModeIs(VRChatTrackingPlan.DisabledMode);
        var builtInWhen = _aap.LipSyncModeIs(VRChatTrackingPlan.BuiltInMode);
        EnsureParameters(controller, cancellers.Count > 0, cancellerConditions, disabledWhen, builtInWhen);

        var origin = LayoutOrigin;
        var xStep = AnimatorGraph.PositionXStep;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, "Lip Sync", layerPriority);
        layer.StateMachine!.ExitPosition += new Vector3(xStep, 0, 0);

        var evaluationState = _graph.AddState(layer, "Mode Evaluation", origin);
        _graph.AsPassThrough(evaluationState);
        SetLipSyncTracking(evaluationState, false);

        var defaultState = _graph.AddInitialDelayState(
            layer,
            origin + new Vector3(0, yStep * 2, 0));
        SetLipSyncTracking(defaultState, false);
        _graph.AddExitTimeTransition(defaultState, evaluationState);

        var mmdState = _mmdSupport.AddPassThroughState(
            layer,
            origin - new Vector3(0, yStep * 2, 0),
            evaluationState);
        if (mmdState != null) SetLipSyncTracking(mmdState, false);
        InstallDisabledState(
            layer,
            disabledWhen,
            evaluationState,
            origin + new Vector3(xStep, 0, 0));
        InstallBuiltInState(
            layer,
            builtInWhen,
            evaluationState,
            origin + new Vector3(xStep, yStep, 0));
        InstallCancellers(
            layer,
            evaluationState,
            cancellers,
            xStep,
            yStep);
    }

    private void EnsureParameters(
        VirtualAnimatorController controller,
        bool hasCancellers,
        ImmutableList<DnfCondition> cancellerConditions,
        DnfCondition disabledWhen,
        DnfCondition builtInWhen)
    {
        _aap.EnsureLipSyncParameters(controller);
        if (hasCancellers)
            controller.EnsureFloatParameterExists(VoiceParameterName);
        AnimatorGraph.EnsureConditionParameters(
            controller,
            cancellerConditions
                .Append(disabledWhen)
                .Append(builtInWhen)
                .Append(_mmdSupport.LayerPlaybackWhen)
                .ToArray());
    }

    private void InstallDisabledState(
        VirtualLayer layer,
        DnfCondition disabledWhen,
        VirtualState evaluationState,
        Vector3 position)
    {
        if (disabledWhen.IsNever) return;

        var disabled = _graph.AddState(layer, "Disabled", position);
        _graph.AsPassThrough(disabled);
        SetLipSyncTracking(disabled, false);
        _graph.AddStateTransition(evaluationState, disabled, disabledWhen, 0f);
        _graph.AddStateTransition(disabled, evaluationState, disabledWhen.Complement(), 0f);
    }

    private void InstallBuiltInState(
        VirtualLayer layer,
        DnfCondition builtInWhen,
        VirtualState evaluationState,
        Vector3 position)
    {
        var builtIn = _graph.AddState(layer, "Built-in", position);
        _graph.AsPassThrough(builtIn);
        SetLipSyncTracking(builtIn, true);
        _graph.AddStateTransition(evaluationState, builtIn, builtInWhen, 0f);
        _graph.AddStateTransition(builtIn, evaluationState, builtInWhen.Complement(), 0f);
    }

    private void InstallCancellers(
        VirtualLayer layer,
        VirtualState evaluationState,
        ImmutableList<(LipSyncSettings Settings, int Mode)> cancellers,
        float xStep,
        float yStep)
    {
        var voiceActiveWhen = VoiceActiveWhen();
        var position = LayoutOrigin + new Vector3(xStep, yStep * 2, 0);
        for (var index = 0; index < cancellers.Count; index++)
        {
            var entry = cancellers[index];
            var when = _aap.LipSyncModeIs(entry.Mode);
            var name = $"Canceller {index + 1}";
            var idle = _graph.AddState(layer, $"Built-in ({name} Idle)", position);
            var lipSyncing = _graph.AddState(
                layer,
                $"Built-in ({name} Lip Syncing)",
                position + new Vector3(0, yStep, 0));
            _graph.AsPassThrough(idle);
            SetLipSyncTracking(idle, true);
            SetCancellerClip(lipSyncing, entry.Settings);

            _graph.AddStateTransition(evaluationState, idle, when, 0f);
            _graph.AddStateTransition(idle, evaluationState, when.Complement(), 0f);
            _graph.AddStateTransition(lipSyncing, evaluationState, when.Complement(), 0f);
            _graph.AddStateTransition(
                idle,
                lipSyncing,
                voiceActiveWhen,
                CancellerTransitionDurationSeconds);
            _graph.AddStateTransition(
                lipSyncing,
                idle,
                voiceActiveWhen.Complement(),
                CancellerTransitionDurationSeconds);

            position.y += yStep * 2;
        }
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

    private static void SetLipSyncTracking(VirtualState state, bool isTracking)
    {
        var control = state.EnsureBehavior<VRCAnimatorTrackingControl>();
        control.trackingMouth = isTracking
            ? VRCAnimatorTrackingControl.TrackingType.Tracking
            : VRCAnimatorTrackingControl.TrackingType.Animation;
    }
}
