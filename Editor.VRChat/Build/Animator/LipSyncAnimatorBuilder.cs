using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>FaceTuneのtracking設定から単一のglobal LipSync layerを構築する。</summary>
internal sealed class LipSyncAnimatorBuilder
{
    private const float InitialRetryDurationSeconds = 0.1f;
    private const float CancellerTransitionDurationSeconds = 0.05f;
    private const string VoiceParameterName = "Voice";
    private const float VoiceThreshold = 0.01f;

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly MmdSupport _mmdSupport;
    private readonly AapProtocol _aap;
    private readonly DnfCondition? _disableWhen;

    public bool ShouldBuild => _aap.ControlsLipSync || _disableWhen != null;

    public LipSyncAnimatorBuilder(
        AvatarContext avatarContext,
        AnimatorGraph graph,
        MmdSupport mmdSupport,
        AapProtocol aap,
        DnfCondition? disableWhen)
    {
        _avatarContext = avatarContext;
        _graph = graph;
        _mmdSupport = mmdSupport;
        _aap = aap;
        _disableWhen = disableWhen;
    }

    public void Build(
        VirtualAnimatorController controller,
        int layerPriority)
    {
        if (!ShouldBuild) return;

        var enabled = _aap.ControlsLipSync
            ? _aap.LipSyncEnabledWhen
            : DnfCondition.Always;
        if (_disableWhen != null) enabled = enabled.And(_disableWhen.Complement());

        var cancellers = _aap.LipSyncCancellerModes;
        _aap.EnsureLipSyncParameters(controller);
        if (cancellers.Count > 0)
            controller.EnsureFloatParameterExists(VoiceParameterName);
        AnimatorGraph.EnsureConditionParameters(
            controller,
            _disableWhen,
            _mmdSupport.LayerPlaybackWhen);

        var origin = AnimatorGraph.DefaultStatePosition;
        var xStep = AnimatorGraph.PositionXStep;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, "Lip Sync", layerPriority);
        layer.StateMachine!.ExitPosition += new Vector3(xStep, 0, 0);

        var initial = _graph.AddState(layer, "Initial", origin);
        _graph.AsPassThrough(initial);
        layer.StateMachine!.DefaultState = initial;
        _graph.SetExitTransitions(initial, DnfCondition.Always, InitialRetryDurationSeconds);

        _mmdSupport.AddPassThroughState(
            layer,
            origin - new Vector3(0, yStep * 2, 0));
        InstallDisabledState(
            layer,
            enabled.Complement(),
            origin + new Vector3(0, yStep * 2, 0));
        var builtIn = InstallBuiltInState(
            layer,
            enabled,
            origin + new Vector3(0, yStep * 3, 0));
        InstallCancellers(layer, builtIn, enabled, cancellers, xStep, yStep);
    }

    private void InstallDisabledState(
        VirtualLayer layer,
        DnfCondition disabledWhen,
        Vector3 position)
    {
        if (disabledWhen.IsNever) return;

        var disabled = _graph.AddState(layer, "Disabled", position);
        _graph.AsPassThrough(disabled);
        SetLipSyncTracking(disabled, false);
        _graph.AddEntryTransition(layer, disabled, disabledWhen);
        _graph.SetExitTransitions(disabled, disabledWhen.Complement(), 0f);
    }

    private VirtualState InstallBuiltInState(
        VirtualLayer layer,
        DnfCondition builtInWhen,
        Vector3 position)
    {
        var builtIn = _graph.AddState(layer, "Built-in", position);
        _graph.AsPassThrough(builtIn);
        SetLipSyncTracking(builtIn, true);
        _graph.AddEntryTransition(layer, builtIn, builtInWhen);
        _graph.SetExitTransitions(builtIn, builtInWhen.Complement(), 0f);
        return builtIn;
    }

    private void InstallCancellers(
        VirtualLayer layer,
        VirtualState builtIn,
        DnfCondition enabled,
        IReadOnlyList<(LipSyncSettings Settings, int Mode)> cancellers,
        float xStep,
        float yStep)
    {
        var voiceActiveWhen = VoiceActiveWhen();
        var position = AnimatorGraph.DefaultStatePosition
            + new Vector3(xStep, yStep * 3, 0);
        for (var index = 0; index < cancellers.Count; index++)
        {
            var entry = cancellers[index];
            var when = enabled.And(_aap.LipSyncModeIs(entry.Mode));
            var name = $"Canceller {index + 1}";
            var idle = _graph.AddState(layer, $"{name} Idle", position);
            var lipSyncing = _graph.AddState(
                layer,
                $"{name} Lip Syncing",
                position + new Vector3(xStep, 0, 0));
            _graph.AsPassThrough(idle);
            SetCancellerClip(lipSyncing, entry.Settings);

            _graph.AddStateTransition(builtIn, idle, when, 0f);
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
            _graph.AddExitTransitions(idle, when.Complement(), 0f);
            _graph.AddExitTransitions(lipSyncing, when.Complement(), 0f);

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
