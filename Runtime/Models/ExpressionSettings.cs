namespace Aoyon.FaceTune;

/// <summary>値を直接持つか、指定Transform上の同種設定を参照するか。</summary>
internal enum SettingsReferenceMode
{
    Direct,
    Reference
}

/// <summary>設定値とは独立してシリアライズする参照情報。</summary>
[Serializable]
internal sealed class SettingsReference
{
    public SettingsReferenceMode Mode = SettingsReferenceMode.Direct;
    public Transform? Source;
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

/// <summary>platform標準Blinkか、FaceTune生成animationか。</summary>
[Serializable]
internal class EyeBlinkSettings
{
    public enum Kind
    {
        BuiltIn,
        CustomAnimation,
        SimpleAnimation
    }

    public Kind EyeBlinkMode = Kind.BuiltIn;

    // for animation modes
    public Vector2 IntervalSeconds = new(4f, 20f);

    // for Kind.SimpleAnimation (x: closing, y: hold, z: opening)
    public Vector3 SimpleDurationsSeconds = new(.07f, 0f, .07f);
    public List<BlendShapeWeight> SimpleBlinkBlendShapes = new() { CreateDefaultBlinkBlendShape() };
    public List<BlendShapeWeight> SimpleConflictPreventionBlendShapes = new();

    internal static BlendShapeWeight CreateDefaultBlinkBlendShape()
        => new("vrc.blink", 100f);

    // for Kind.CustomAnimation
    public List<BlendShapeWeightAnimation> Animations = new() { CreateDefaultAnimation() };

    internal static BlendShapeWeightAnimation CreateDefaultAnimation()
        => new("vrc.blink", new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.07f, 100f), new Keyframe(.14f, 0f)));
}

/// <summary>LipSyncと競合するBlendShapeの打ち消し設定。</summary>
[Serializable]
internal class LipSyncSettings
{
    public List<BlendShapeWeight> CancellerBlendShapes = new();
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

/// <summary>Serialized tracking permission shared by current and legacy data.</summary>
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
        Parameter,
        Menu
    }

    public Kind MultiFrameMode = Kind.Default;

    public Hand TriggerHand = Hand.Left; // For Kind.Trigger
    public string ParameterName = string.Empty; // For Kind.Parameter
    public MenuComponent? MenuSource = null; // For Kind.Menu
}