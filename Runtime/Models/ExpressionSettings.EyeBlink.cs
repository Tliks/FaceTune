namespace Aoyon.FaceTune;

internal enum EyeBlinkMode { BuiltIn, Automatic }

[Serializable]
internal record class EyeBlinkSettings
{
    [SerializeField] private EyeBlinkMode mode = EyeBlinkMode.BuiltIn;
    [SerializeField] private AutomaticBlinkSettings automatic = new();
    public EyeBlinkMode Mode { get => mode; init => mode = value; }
    public AutomaticBlinkSettings Automatic { get => automatic; init => automatic = value; }
    internal bool IsAutomatic() => mode == EyeBlinkMode.Automatic;
}

[Serializable]
internal record class AutomaticBlinkSettings
{
    [SerializeField] private List<BlendShapeWeightAnimation> animations = CreateDefaultAnimations();
    [SerializeField] private FloatRange intervalSeconds = new(3f, 7f);
    public IReadOnlyList<BlendShapeWeightAnimation> Animations { get => animations.AsReadOnly(); init => animations = value.ToList(); }
    public FloatRange IntervalSeconds { get => intervalSeconds; init => intervalSeconds = value; }

    internal static List<BlendShapeWeightAnimation> CreateDefaultAnimations() => new()
    {
        new BlendShapeWeightAnimation("vrc.blink", new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.06666667f, 100f), new Keyframe(.13333334f, 0f)))
    };
}

[Serializable]
internal struct FloatRange
{
    [SerializeField] private float min;
    [SerializeField] private float max;
    public float Min { get => min; init => min = value; }
    public float Max { get => max; init => max = value; }
    public FloatRange(float min, float max) => (this.min, this.max) = (min, max);
}
