namespace Aoyon.FaceTune.Build.Animator;

internal sealed record class AnimatorBuildPlan(
    InitialLayerPlan InitialLayer,
    IReadOnlyList<OutputUnitPlan> Units,
    TrackingControlLayerPlan? TrackingControlLayer);

internal sealed record class InitialLayerPlan(
    string Name,
    Transform Anchor,
    InitialStatePlan DefaultState,
    IReadOnlyList<InitialStatePlan> States,
    IReadOnlyList<PlanParameter> Parameters);

// DefaultState.When is its exit condition; States[].When is each state's entry condition.
internal sealed record class InitialStatePlan(
    string Name,
    DnfCondition When,
    IReadOnlyList<BlendShapeWeight> BlendShapes);

internal sealed record class OutputUnitPlan(
    int Id,
    Transform Anchor,
    IReadOnlyList<ExpressionLayerPlan> ExpressionLayers,
    AdvancedEyeBlinkLayerPlan? AdvancedEyeBlink,
    AdvancedLipSyncLayerPlan? AdvancedLipSync,
    IReadOnlyList<PlanParameter> Parameters);

internal sealed record class ExpressionLayerPlan(
    string Name,
    float TransitionDurationSeconds,
    DnfCondition? PassThroughExitWhen,
    DnfCondition? ForceInactiveWhen,
    IReadOnlyList<ExpressionStatePlan> States);

internal sealed record class ExpressionStatePlan(
    string Name,
    DnfCondition EnterWhen,
    DnfCondition ExitWhen,
    BlendShapeWeightAnimationSet Animations,
    MultiFrameSettings Settings,
    IReadOnlyList<AapWrite> AapWrites);

internal readonly record struct AapWrite(string ParameterName, float Value);

internal sealed record class TrackingControlLayerPlan(
    string Name,
    Transform Anchor,
    DnfCondition DefaultExitWhen,
    TrackingControlStatePlan DefaultState,
    DnfCondition? ForceInactiveWhen,
    IReadOnlyList<TrackingControlStatePlan> States,
    IReadOnlyList<PlanParameter> Parameters);

internal sealed record class TrackingControlStatePlan(
    string Name,
    DnfCondition When,
    bool? EyeBlinkTracking,
    bool? LipSyncTracking);

internal sealed record class AdvancedEyeBlinkLayerPlan(string Name, DnfCondition? ForceInactiveWhen);

internal sealed record class AdvancedLipSyncLayerPlan(string Name, DnfCondition? ForceInactiveWhen);

internal readonly record struct PlanParameter(
    string Name,
    AnimatorControllerParameterType Type,
    float DefaultValue);
