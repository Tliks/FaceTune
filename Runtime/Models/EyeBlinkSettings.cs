namespace Aoyon.FaceTune;

internal enum EyeBlinkMode
{
    BuiltIn,
    Automatic
}

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
    // 瞬き1回分。Clipを取り込んだ場合もここへ変換する。
    [SerializeField] private List<BlendShapeWeightAnimation> animations = new();
    [SerializeField] private FloatRange intervalSeconds = new(3f, 7f);

    public IReadOnlyList<BlendShapeWeightAnimation> Animations
    {
        get => animations.AsReadOnly();
        init => animations = value.ToList();
    }

    public FloatRange IntervalSeconds { get => intervalSeconds; init => intervalSeconds = value; }
}
