using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>単一のglobal LipSync layerを構築する。</summary>
internal sealed class LipSyncAnimatorInstaller
{
    private const float InitialRetryDurationSeconds = 0.1f;
    private const float CancellerTransitionDurationSeconds = 0.05f;

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;

    public LipSyncAnimatorInstaller(AvatarContext avatarContext, AnimatorGraph graph)
    {
        _avatarContext = avatarContext;
        _graph = graph;
    }

    public void Install(
        VirtualAnimatorController controller,
        LipSyncLayerPlan plan,
        int layerPriority)
    {
        AnimatorGraph.EnsureParameters(controller, plan.Parameters);
        var origin = AnimatorGraph.DefaultStatePosition;
        var xStep = AnimatorGraph.PositionXStep;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, plan.Name, layerPriority);
        layer.StateMachine!.ExitPosition += new Vector3(xStep, 0, 0);

        var initial = _graph.AddState(layer, "Initial", origin);
        _graph.AsPassThrough(initial);
        layer.StateMachine!.DefaultState = initial;
        _graph.SetExitTransitions(
            initial,
            plan.InitialExitWhen,
            InitialRetryDurationSeconds);

        InstallMmdState(layer, plan, origin, yStep);
        InstallDisabledState(layer, plan, origin + new Vector3(0, yStep * 2, 0));
        var builtIn = InstallBuiltInState(
            layer,
            plan,
            origin + new Vector3(0, yStep * 3, 0));
        InstallCancellers(layer, builtIn, plan, xStep, yStep);
    }

    private void InstallMmdState(
        VirtualLayer layer,
        LipSyncLayerPlan plan,
        Vector3 origin,
        float yStep)
    {
        if (plan.MmdPlaybackWhen is not { } mmdWhen)
        {
            return;
        }

        var mmd = _graph.AddState(
            layer,
            "MMD Playback",
            origin - new Vector3(0, yStep * 2, 0));
        _graph.AsPassThrough(mmd);
        _graph.SetAnyStateTransition(layer, mmd, mmdWhen, 0f);
        _graph.SetExitTransitions(mmd, mmdWhen.Complement(), 0f);
    }

    private void InstallDisabledState(
        VirtualLayer layer,
        LipSyncLayerPlan plan,
        Vector3 position)
    {
        var disabled = _graph.AddState(layer, "Disabled", position);
        _graph.AsPassThrough(disabled);
        if (plan.UseTrackingControl)
        {
            SetLipSyncTracking(disabled, false);
        }
        _graph.AddEntryTransition(layer, disabled, plan.DisabledWhen);
        _graph.SetExitTransitions(
            disabled,
            plan.DisabledWhen.Complement(),
            0f);
    }

    private VirtualState InstallBuiltInState(
        VirtualLayer layer,
        LipSyncLayerPlan plan,
        Vector3 position)
    {
        var builtIn = _graph.AddState(layer, "Built-in", position);
        _graph.AsPassThrough(builtIn);
        if (plan.UseTrackingControl)
        {
            SetLipSyncTracking(builtIn, true);
        }
        _graph.AddEntryTransition(layer, builtIn, plan.BuiltInWhen);
        _graph.SetExitTransitions(
            builtIn,
            plan.BuiltInWhen.Complement(),
            0f);
        return builtIn;
    }

    private void InstallCancellers(
        VirtualLayer layer,
        VirtualState builtIn,
        LipSyncLayerPlan plan,
        float xStep,
        float yStep)
    {
        var position = AnimatorGraph.DefaultStatePosition
            + new Vector3(xStep, yStep * 3, 0);
        foreach (var canceller in plan.Cancellers)
        {
            var idle = _graph.AddState(
                layer,
                $"{canceller.Name} Idle",
                position);
            var lipSyncing = _graph.AddState(
                layer,
                $"{canceller.Name} Lip Syncing",
                position + new Vector3(xStep, 0, 0));
            _graph.AsPassThrough(idle);
            SetCancellerClip(lipSyncing, canceller);

            _graph.AddStateTransition(builtIn, idle, canceller.When, 0f);
            _graph.AddStateTransition(
                idle,
                lipSyncing,
                plan.VoiceActiveWhen,
                CancellerTransitionDurationSeconds);
            _graph.AddStateTransition(
                lipSyncing,
                idle,
                plan.VoiceInactiveWhen,
                CancellerTransitionDurationSeconds);
            _graph.AddExitTransitions(
                idle,
                canceller.When.Complement(),
                0f);
            _graph.AddExitTransitions(
                lipSyncing,
                canceller.When.Complement(),
                0f);

            position.y += yStep * 2;
        }
    }

    private void SetCancellerClip(
        VirtualState state,
        LipSyncCancellerPlan plan)
    {
        state.SetNewClip(state.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            plan.BlendShapes.ToBlendShapeAnimations());
    }

    private static void SetLipSyncTracking(VirtualState state, bool isTracking)
    {
        var control = state.EnsureBehavior<VRCAnimatorTrackingControl>();
        control.trackingMouth = isTracking
            ? VRCAnimatorTrackingControl.TrackingType.Tracking
            : VRCAnimatorTrackingControl.TrackingType.Animation;
    }
}
