namespace com.aoyon.facetune;

[Serializable]
public record class ExpressionSettings // Immutable
{
    // Advanced
    public bool IsLoop;
    public string MotionTimeParameterName; // IsLoop == false && != empty

    public ExpressionSettings()
    {
        IsLoop = false;
        MotionTimeParameterName = string.Empty;
    }

    public ExpressionSettings(bool isLoop, string motionTimeParameterName)
    {
        IsLoop = isLoop;
        MotionTimeParameterName = motionTimeParameterName;
    }

    internal static ExpressionSettings FromAnimationClip(AnimationClip clip)
    {
        // 若干の損失が発生している
        switch (clip.wrapMode)
        {
            case WrapMode.Loop:
                return new ExpressionSettings(true, string.Empty);
            default:
                return new ExpressionSettings(false, string.Empty);
        }
    }

    internal ExpressionSettings Merge(ExpressionSettings other)
    {
        var isLoop = IsLoop || other.IsLoop;
        var motionTimeParameterName = string.IsNullOrEmpty(other.MotionTimeParameterName) ? MotionTimeParameterName : other.MotionTimeParameterName;
        return new ExpressionSettings(isLoop, motionTimeParameterName);
    }
}