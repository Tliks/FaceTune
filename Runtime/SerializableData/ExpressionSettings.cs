namespace com.aoyon.facetune;

[Serializable]
public record class ExpressionSettings // Immutable
{
    // Advanced
    public bool LoopTime;
    public string MotionTimeParameterName; // LoopTime == false && != empty

    public ExpressionSettings()
    {
        LoopTime = false;
        MotionTimeParameterName = string.Empty;
    }

    public ExpressionSettings(bool loopTime, string motionTimeParameterName)
    {
        LoopTime = loopTime;
        MotionTimeParameterName = motionTimeParameterName;
    }

#if UNITY_EDITOR
    internal static ExpressionSettings FromAnimationClip(AnimationClip clip)
    {
        var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
        return new ExpressionSettings(settings.loopTime, "");
    }
#endif

    internal ExpressionSettings Merge(ExpressionSettings other)
    {
        var loopTime = LoopTime || other.LoopTime;
        var motionTimeParameterName = string.IsNullOrEmpty(other.MotionTimeParameterName) ? MotionTimeParameterName : other.MotionTimeParameterName;
        return new ExpressionSettings(loopTime, motionTimeParameterName);
    }
}