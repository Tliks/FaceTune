namespace Aoyon.FaceTune.Preview;

internal sealed class SelectedPreviewData
{
    internal SelectedPreviewData(IReadOnlyList<AvatarPreviewData> avatars)
    {
        Avatars = avatars;
    }

    internal IReadOnlyList<AvatarPreviewData> Avatars { get; }
}

internal sealed class AvatarPreviewData
{
    internal AvatarPreviewData(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        ExpressionComponent? expression,
        FacialPreviewData facial,
        EyeBlinkPreviewData? eyeBlink,
        LipSyncPreviewData? lipSync)
    {
        Root = root;
        FaceRenderer = faceRenderer;
        Expression = expression;
        Facial = facial;
        EyeBlink = eyeBlink;
        LipSync = lipSync;
    }

    internal GameObject Root { get; }
    internal SkinnedMeshRenderer FaceRenderer { get; }
    internal ExpressionComponent? Expression { get; }
    internal FacialPreviewData Facial { get; }
    internal EyeBlinkPreviewData? EyeBlink { get; }
    internal LipSyncPreviewData? LipSync { get; }
}

internal sealed class FacialPreviewData
{
    internal FacialPreviewData(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        float? defaultWeight,
        ImmutableHashSet<string> ignoredNames,
        bool isLooping)
    {
        Animations = animations;
        DefaultWeight = defaultWeight;
        IgnoredNames = ignoredNames;

        var duration = BlendShapeAnimationPreview.GetDuration(animations);
        var hasMultipleFrames = animations.Any(animation => animation.IsMultiFrame);
        if (duration > 0f && hasMultipleFrames)
            MultiFrame = new MultiFramePreviewData(duration, isLooping);
    }

    internal IReadOnlyList<BlendShapeWeightAnimation> Animations { get; }
    internal float? DefaultWeight { get; }
    internal ImmutableHashSet<string> IgnoredNames { get; }
    internal MultiFramePreviewData? MultiFrame { get; }
}

internal sealed record MultiFramePreviewData(float Duration, bool IsLooping);

internal sealed class EyeBlinkPreviewData
{
    private EyeBlinkPreviewData() { }

    internal float Duration { get; private init; }
    internal ImmutableBlendShapeWeightSet? ClosedShapes { get; private init; }
    internal Vector3 SimpleDurations { get; private init; }
    internal Vector2? ClosedRange { get; private init; }
    internal IReadOnlyList<BlendShapeWeightAnimation> Animations { get; private init; }
        = Array.Empty<BlendShapeWeightAnimation>();

    internal static EyeBlinkPreviewData? FromSimple(EyeBlinkSettings settings)
    {
        var closed = new BlendShapeWeightSet(settings.SimpleBlinkBlendShapes);
        closed.AddRange(settings.SimpleConflictPreventionBlendShapes);
        var durations = settings.SimpleDurationsSeconds;
        var closing = Mathf.Max(0f, durations.x);
        var hold = Mathf.Max(0f, durations.y);
        var opening = Mathf.Max(0f, durations.z);
        var duration = closing + hold + opening;
        if (duration <= 0f || closed.Count == 0) return null;
        return new EyeBlinkPreviewData
        {
            Duration = duration,
            ClosedShapes = new ImmutableBlendShapeWeightSet(closed),
            SimpleDurations = new Vector3(closing, hold, opening),
            ClosedRange = new Vector2(closing / duration, (closing + hold) / duration)
        };
    }

    internal static EyeBlinkPreviewData? FromAnimation(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        Vector2? closedRange = null)
    {
        var duration = BlendShapeAnimationPreview.GetDuration(animations);
        if (duration <= 0f) return null;
        return new EyeBlinkPreviewData
        {
            Duration = duration,
            Animations = animations,
            ClosedRange = closedRange
        };
    }

    internal float SimpleOpacity(float normalizedTime)
    {
        var time = Mathf.Clamp01(normalizedTime) * Duration;
        if (time < SimpleDurations.x)
            return SimpleDurations.x <= 0f ? 1f : time / SimpleDurations.x;
        time -= SimpleDurations.x;
        if (time <= SimpleDurations.y) return 1f;
        time -= SimpleDurations.y;
        if (SimpleDurations.z <= 0f) return 0f;
        return 1f - Mathf.Clamp01(time / SimpleDurations.z);
    }
}

internal sealed class LipSyncPreviewData
{
    internal static IReadOnlyList<string> VisemeNames { get; } = new[]
    {
        "sil", "PP", "FF", "TH", "DD",
        "kk", "CH", "SS", "nn", "RR",
        "aa", "E", "ih", "oh", "ou"
    };

    internal static int VisemeCount => VisemeNames.Count;

    internal LipSyncPreviewData(
        IEnumerable<BlendShapeWeight> canceller,
        VrcVisemeLipSyncShapes shapes)
    {
        Canceller = new ImmutableBlendShapeWeightSet(canceller);
        var visemes = new[]
        {
            shapes.Sil, shapes.PP, shapes.FF, shapes.TH, shapes.DD,
            shapes.KK, shapes.CH, shapes.SS, shapes.NN, shapes.RR,
            shapes.AA, shapes.E, shapes.IH, shapes.OH, shapes.OU
        };
        Visemes = visemes
            .Select(values => new ImmutableBlendShapeWeightSet(values))
            .ToArray();
    }

    internal ImmutableBlendShapeWeightSet Canceller { get; }
    internal ImmutableBlendShapeWeightSet[] Visemes { get; }
}
