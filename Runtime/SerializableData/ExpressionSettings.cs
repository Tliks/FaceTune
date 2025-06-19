namespace com.aoyon.facetune;

[Serializable]
public record class ExpressionSettings // Immutable
{
    // Advanced
    [SerializeField] private bool _loopTime;
    public const string LoopTimePropName = "_loopTime";
    public bool LoopTime { get => _loopTime; init => _loopTime = value; }

    [SerializeField] private string _motionTimeParameterName;
    public const string MotionTimeParameterNamePropName = "_motionTimeParameterName";
    public string MotionTimeParameterName { get => _motionTimeParameterName; init => _motionTimeParameterName = value; } // LoopTime == false && != empty

    public ExpressionSettings()
    {
        _loopTime = false;
        _motionTimeParameterName = string.Empty;
    }

    public ExpressionSettings(bool loopTime, string motionTimeParameterName)
    {
        _loopTime = loopTime;
        _motionTimeParameterName = motionTimeParameterName;
    }

#if UNITY_EDITOR
    internal static ExpressionSettings FromAnimationClip(AnimationClip clip)
    {
        var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
        return new ExpressionSettings(settings.loopTime, "");
    }
#endif

    // Todo
    internal ExpressionSettings Merge(ExpressionSettings other)
    {
        var loopTime = _loopTime || other._loopTime;
        var motionTimeParameterName = string.IsNullOrEmpty(other._motionTimeParameterName) ? _motionTimeParameterName : other._motionTimeParameterName;
        return new ExpressionSettings(loopTime, motionTimeParameterName);
    }
}