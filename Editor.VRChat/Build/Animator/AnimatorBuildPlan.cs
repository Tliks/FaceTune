namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed record class AnimatorBuildPlan(
    InitialLayerPlan InitialLayer,
    IReadOnlyList<OutputUnitPlan> Units,
    Transform ControlAnchor,
    int ControlPriority,
    EyeBlinkLayerPlan? EyeBlinkLayer,
    LipSyncLayerPlan? LipSyncLayer);

internal sealed record class InitialLayerPlan(
    string Name,
    int Priority,
    Transform Anchor,
    InitialStatePlan DefaultState,
    InitialStatePlan? MmdPlaybackState,
    MmdDisableMode MmdDisableMode,
    IReadOnlyList<PlanParameter> Parameters);

// DefaultState.When is its exit condition; MmdPlaybackState.When is its entry condition.
internal sealed record class InitialStatePlan(
    string Name,
    DnfCondition When,
    IReadOnlyList<BlendShapeWeight> BlendShapes);

internal enum MmdDisableMode
{
    None,
    DisableLayers,
    DisableFxLayer
}

internal readonly record struct MmdAnimatorPolicy(
    DnfCondition? PlaybackWhen,
    MmdDisableMode DisableMode);

internal sealed record class OutputUnitPlan(
    int Id,
    int Priority,
    Transform Anchor,
    IReadOnlyList<ExpressionLayerPlan> ExpressionLayers,
    IReadOnlyList<PlanParameter> Parameters);

internal sealed record class ExpressionLayerPlan(
    string Name,
    float TransitionDurationSeconds,
    DnfCondition InitialExitWhen,
    DnfCondition? PassThroughWhen,
    DnfCondition? MmdPlaybackWhen,
    IReadOnlyList<ExpressionStatePlan> States);

internal sealed record class ExpressionStatePlan(
    string Name,
    DnfCondition EnterWhen,
    DnfCondition ExitWhen,
    BlendShapeWeightAnimationSet Animations,
    MultiFrameSettings Settings,
    IReadOnlyList<AapWrite> AapWrites);

internal readonly record struct AapWrite(string ParameterName, float Value);

internal sealed record class EyeBlinkLayerPlan(
    string Name,
    bool UseTrackingControl,
    DnfCondition InitialExitWhen,
    DnfCondition DisabledWhen,
    DnfCondition BuiltInWhen,
    DnfCondition AnimationWhen,
    DnfCondition? MmdPlaybackWhen,
    IReadOnlyList<EyeBlinkAnimationPlan> Animations,
    IReadOnlyList<PlanParameter> Parameters);

internal sealed record class EyeBlinkAnimationPlan(
    string Name,
    EyeBlinkSettings.Kind Kind,
    DnfCondition When,
    Vector2 IntervalSeconds,
    Vector3 SimpleDurationsSeconds,
    IReadOnlyList<BlendShapeWeight> SimpleCloseBlendShapes,
    IReadOnlyList<BlendShapeWeightAnimation> CustomAnimations,
    string SpeedParameterName);

internal sealed record class LipSyncLayerPlan(
    string Name,
    bool UseTrackingControl,
    DnfCondition InitialExitWhen,
    DnfCondition DisabledWhen,
    DnfCondition BuiltInWhen,
    DnfCondition VoiceActiveWhen,
    DnfCondition VoiceInactiveWhen,
    DnfCondition? MmdPlaybackWhen,
    IReadOnlyList<LipSyncCancellerPlan> Cancellers,
    IReadOnlyList<PlanParameter> Parameters);

internal sealed record class LipSyncCancellerPlan(
    string Name,
    DnfCondition When,
    IReadOnlyList<BlendShapeWeight> BlendShapes);

internal readonly record struct PlanParameter(
    string Name,
    AnimatorControllerParameterType Type,
    float DefaultValue);
