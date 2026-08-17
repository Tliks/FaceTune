using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>controller単位のinstall順序を管理する。</summary>
internal sealed class AnimatorInstaller
{
    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly ExpressionAnimatorInstaller _expressions;
    private readonly EyeBlinkAnimatorInstaller _eyeBlink;
    private readonly LipSyncAnimatorInstaller _lipSync;

    public AnimatorInstaller(AvatarContext avatarContext, bool useWriteDefaults)
    {
        _avatarContext = avatarContext;
        _graph = new AnimatorGraph(useWriteDefaults);
        _expressions = new ExpressionAnimatorInstaller(avatarContext, _graph);
        _eyeBlink = new EyeBlinkAnimatorInstaller(avatarContext, _graph);
        _lipSync = new LipSyncAnimatorInstaller(avatarContext, _graph);
    }

    public void InstallInitial(
        VirtualAnimatorController controller,
        InitialLayerPlan plan,
        int layerPriority)
    {
        AnimatorGraph.EnsureParameters(controller, plan.Parameters);
        var origin = AnimatorGraph.DefaultStatePosition;
        var layer = _graph.AddLayer(controller, plan.Name, layerPriority);
        var defaultState = _graph.AddState(layer, plan.DefaultState.Name, origin);
        layer.StateMachine!.DefaultState = defaultState;
        SetInitialClip(defaultState, plan.DefaultState);
        _graph.SetExitTransitions(defaultState, plan.DefaultState.When, 0f);

        if (plan.MmdPlaybackState is not { } mmdPlan) return;

        var mmdState = _graph.AddState(
            layer,
            mmdPlan.Name,
            origin + new Vector3(0, AnimatorGraph.PositionYStep * 2, 0));
        SetInitialClip(mmdState, mmdPlan);
        _graph.AddEntryTransition(layer, mmdState, mmdPlan.When);
        _graph.SetExitTransitions(mmdState, mmdPlan.When.Complement(), 0f);

        if (plan.MmdDisableMode == MmdDisableMode.DisableFxLayer)
        {
            SetFxPlayableWeight(defaultState, 1f);
            SetFxPlayableWeight(mmdState, 0f);
        }
    }

    public void InstallUnit(
        VirtualAnimatorController controller,
        OutputUnitPlan unit,
        int layerPriority)
    {
        AnimatorGraph.EnsureParameters(controller, unit.Parameters);
        foreach (var layer in unit.ExpressionLayers)
            _expressions.Install(controller, layer, layerPriority);
    }

    public void InstallEyeBlink(
        VirtualAnimatorController controller,
        EyeBlinkLayerPlan plan,
        int layerPriority)
        => _eyeBlink.Install(controller, plan, layerPriority);

    public void InstallLipSync(
        VirtualAnimatorController controller,
        LipSyncLayerPlan plan,
        int layerPriority)
        => _lipSync.Install(controller, plan, layerPriority);

    private void SetInitialClip(VirtualState state, InitialStatePlan plan)
    {
        state.SetNewClip(plan.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            plan.BlendShapes.ToBlendShapeAnimations());
    }

    private static void SetFxPlayableWeight(VirtualState state, float weight)
    {
        var control = state.EnsureBehavior<VRCPlayableLayerControl>();
        control.layer = VRCPlayableLayerControl.BlendableLayer.FX;
        control.goalWeight = weight;
        control.blendDuration = 0f;
    }
}
