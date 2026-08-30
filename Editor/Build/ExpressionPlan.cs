namespace Aoyon.FaceTune.Build;

/// <summary>Component hierarchy interpreted as FaceTune expressions for build backends.</summary>
internal sealed class ExpressionPlan
{
    public ImmutableList<ExpressionItem> Items { get; }

    public bool IsEmpty => Items.Count == 0;

    public ExpressionPlan(IEnumerable<ExpressionItem> items)
    {
        Items = items.ToImmutableList();
    }
}

internal sealed record class ExpressionItem(
    Transform SourceTransform,
    string Name,
    BlendShapeWeightAnimationSet IncomingFacialAnimations,
    BlendShapeWeightAnimationSet LocalFacialAnimations,
    ResolvedNonFacialAnimationSet NonFacialAnimations,
    ExpressionWriteMode WriteMode,
    MultiFrameSettings MultiFrame,
    TrackingPermission AllowEyeBlink,
    TrackingPermission AllowLipSync,
    EyeBlinkSettings EyeBlink,
    LipSyncSettings LipSync,
    TransitionSettings Transition,
    PrioritySettings Priority,
    DnfCondition RawWhen);
