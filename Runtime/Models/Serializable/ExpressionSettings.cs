namespace Aoyon.FaceTune;

[Serializable]
public record class ExpressionSettings // Immutable
{
    // Advanced
    [SerializeField] private bool loopTime;
    public bool LoopTime { get => loopTime; init => loopTime = value; }
    public const string LoopTimePropName = nameof(loopTime);

    [SerializeField] private string motionTimeParameterName;
    public string MotionTimeParameterName { get => motionTimeParameterName; init => motionTimeParameterName = value; } // LoopTime == false && != empty
    public const string MotionTimeParameterNamePropName = nameof(motionTimeParameterName);

    public ExpressionSettings()
    {
        loopTime = false;
        motionTimeParameterName = string.Empty;
    }

    public ExpressionSettings(bool loopTime, string motionTimeParameterName)
    {
        this.loopTime = loopTime;
        this.motionTimeParameterName = motionTimeParameterName;
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
        var loopTime = this.loopTime || other.loopTime;
        var motionTimeParameterName = string.IsNullOrEmpty(other.motionTimeParameterName) ? this.motionTimeParameterName : other.motionTimeParameterName;
        return new ExpressionSettings(loopTime, motionTimeParameterName);
    }

    public virtual bool Equals(ExpressionSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return LoopTime == other.LoopTime && MotionTimeParameterName == other.MotionTimeParameterName;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LoopTime, MotionTimeParameterName);
    }
}