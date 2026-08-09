namespace Aoyon.FaceTune;

/// <summary>値を直接持つか、指定Transform上の同種設定を参照するか。</summary>
internal enum SettingsSourceMode
{
    Direct,
    Reference
}

[Serializable]
internal class FacialBlendShapeDataSource : ISettingsSource<FacialBlendShapeData>
{
    public SettingsSourceMode SourceMode = SettingsSourceMode.Direct;
    public Transform? Source;
    public FacialBlendShapeData Direct = new();

    SettingsSourceMode ISettingsSource<FacialBlendShapeData>.SourceMode => SourceMode;
    Transform? ISettingsSource<FacialBlendShapeData>.Source => Source;
    FacialBlendShapeData ISettingsSource<FacialBlendShapeData>.Direct => Direct;
}

/// <summary>
/// 顔のBlendShape data。Clipの後に手入力を重ねる。
/// 後の同名BlendShapeが前を置き換え、0も明示値として扱う。
/// </summary>
[Serializable]
internal class FacialBlendShapeData
{
    public AnimationClip? Clip = null;
    public ClipImportOption ClipOption = ClipImportOption.NonZero;

    public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();
}

internal enum ClipImportOption
{
    All,
    NonZero
}

[Serializable]
internal class EyeBlinkSettingsSource : ISettingsSource<EyeBlinkSettings>
{
    public SettingsSourceMode SourceMode = SettingsSourceMode.Direct;
    public Transform? Source;
    public EyeBlinkSettings Direct = new();

    SettingsSourceMode ISettingsSource<EyeBlinkSettings>.SourceMode => SourceMode;
    Transform? ISettingsSource<EyeBlinkSettings>.Source => Source;
    EyeBlinkSettings ISettingsSource<EyeBlinkSettings>.Direct => Direct;
}

/// <summary>platform標準Blinkか、FaceTune生成animationか。</summary>
[Serializable]
internal class EyeBlinkSettings
{
    public enum Kind
    {
        BuiltIn,
        Automatic
    }

    public Kind EyeBlinkMode = Kind.BuiltIn;

    // for Kind.Automatic
    public List<BlendShapeWeightAnimation> Animations = CreateDefaultAnimations();
    public Vector2 IntervalSeconds = new(3f, 7f);

    internal static List<BlendShapeWeightAnimation> CreateDefaultAnimations() => new()
    {
        new BlendShapeWeightAnimation("vrc.blink", new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.06666667f, 100f), new Keyframe(.13333334f, 0f)))
    };
}

[Serializable]
internal class LipSyncSettingsSource : ISettingsSource<LipSyncSettings>
{
    public SettingsSourceMode SourceMode = SettingsSourceMode.Direct;
    public Transform? Source;
    public LipSyncSettings Direct = new();

    SettingsSourceMode ISettingsSource<LipSyncSettings>.SourceMode => SourceMode;
    Transform? ISettingsSource<LipSyncSettings>.Source => Source;
    LipSyncSettings ISettingsSource<LipSyncSettings>.Direct => Direct;
}

/// <summary>LipSyncと競合するBlendShapeの打ち消し設定。</summary>
[Serializable]
internal class LipSyncSettings
{
    public List<BlendShapeWeight> CancellerBlendShapes = new();
}

[Serializable]
internal class ParameterDriverSettingsSource : ISettingsSource<ParameterDriverSettings>
{
    public SettingsSourceMode SourceMode = SettingsSourceMode.Direct;
    public Transform? Source;
    public ParameterDriverSettings Direct = new();

    SettingsSourceMode ISettingsSource<ParameterDriverSettings>.SourceMode => SourceMode;
    Transform? ISettingsSource<ParameterDriverSettings>.Source => Source;
    ParameterDriverSettings ISettingsSource<ParameterDriverSettings>.Direct => Direct;
}

/// <summary>表情の発動時に書き込むparameter群。</summary>
[Serializable]
internal class ParameterDriverSettings
{
    public struct Entry
    {
        public string Name;
        public ParameterType Type;
        public float FloatValue;
        public int IntValue;
        public bool BoolValue;
    }

    public List<Entry> Entries = new();
}

/// <summary>表情の遷移時間。</summary>
[Serializable]
internal class TransitionSettings
{
    public float DurationSeconds = 0.1f;
}

/// <summary>表情の優先順位。同値ならHierarchy上で下が優先。VRCではMergeAnimatorの優先度と同一。</summary>
[Serializable]
internal class PrioritySettings
{
    public int Priority = 0;
}

/// <summary>Menuの選択状態を、このSettingsが付いたGameObject以下の条件へ加える。</summary>
[Serializable]
internal class ExpressionSetSettings
{
    public MenuSettings Menu = new();
    public bool DefaultSelected;
}

internal enum TrackingPermission
{
    Allow,
    Disallow,
    Keep
}

internal enum ExpressionWriteMode
{
    Replace,
    Blend
}

/// <summary>表情animationの時間制御。</summary>
[Serializable]
internal class MultiFrameSettings
{
    public enum Kind
    {
        Default,
        Loop,
        Trigger,
        Parameter
    }

    public Kind MultiFrameMode = Kind.Default;

    public Hand TriggerHand = Hand.Left; // For Kind.Trigger
    public string ParameterName = string.Empty; // For Kind.Parameter
}