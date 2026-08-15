namespace Aoyon.FaceTune.Build;

/// <summary>Component hierarchy interpreted as FaceTune expressions for build backends.</summary>
internal sealed class ExpressionProgram
{
    public IReadOnlyList<ExpressionItem> Items { get; }

    public bool IsEmpty => Items.Count == 0;

    public ExpressionProgram(IEnumerable<ExpressionItem> items)
    {
        Items = items.ToArray();
    }
}

internal sealed record class ExpressionItem(
    Transform SourceTransform,
    string Name,
    BlendShapeWeightAnimationSet FacialAnimationSet,
    BlendShapeWeightAnimationSet AnimationSet,
    ExpressionWriteMode WriteMode,
    MultiFrameSettings MultiFrame,
    TrackingPermission AllowEyeBlink,
    TrackingPermission AllowLipSync,
    EyeBlinkSettings EyeBlink,
    LipSyncSettings LipSync,
    TransitionSettings Transition,
    PrioritySettings Priority,
    DnfCondition RawWhen);
