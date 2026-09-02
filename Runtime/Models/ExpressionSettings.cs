namespace Aoyon.FaceTune;

// 現状個別でシリアラウズしているのでそれをまとめているだけ
internal sealed record ExpressionBehavior(
    ExpressionWriteMode WriteMode,
    TrackingPermission AllowEyeBlink,
    TrackingPermission AllowLipSync)
{
    internal static readonly ExpressionBehavior Default = new(
        ExpressionWriteMode.Replace,
        TrackingPermission.Disallow,
        TrackingPermission.Allow);
}

/// <summary>値を直接持つか、指定Transform上の同種設定を参照するか。</summary>
internal enum SettingsReferenceMode
{
    Direct = 0,
    Reference = 10
}

/// <summary>設定値とは独立してシリアライズする参照情報。</summary>
[Serializable]
internal sealed class SettingsReference
{
    public SettingsReferenceMode Mode = SettingsReferenceMode.Direct;
    public Transform? Source;
}

[Serializable]
internal sealed class FacialClipBlendShapeData : IEquatable<FacialClipBlendShapeData>
{
    public AnimationClip? Clip = null;
    public ClipImportOption ClipOption = ClipImportOption.NonZero;

    internal FacialClipBlendShapeData Clone()
        => new()
        {
            Clip = Clip,
            ClipOption = ClipOption
        };

    public bool Equals(FacialClipBlendShapeData? other)
        => other is not null
        && Clip == other.Clip
        && ClipOption == other.ClipOption;

    public override bool Equals(object? obj)
        => obj is FacialClipBlendShapeData other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Clip, ClipOption);
}

/// <summary>
/// 顔のBlendShape data。参照の後にClip、その後に手動入力を重ねる。
/// 後の同名BlendShapeが前を置き換え、0も明示値として扱う。
/// </summary>
[Serializable]
internal class FacialBlendShapeData : IEquatable<FacialBlendShapeData>
{
    public List<Transform> ReferenceAnimations = new();
    public List<FacialClipBlendShapeData> ClipAnimations = new();
    public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

    internal FacialBlendShapeData Clone()
        => new()
        {
            ReferenceAnimations = ReferenceAnimations.ToList(),
            ClipAnimations = ClipAnimations
                .Where(animation => animation != null)
                .Select(animation => animation.Clone())
                .ToList(),
            BlendShapeAnimations = BlendShapeAnimations
                .Select(animation => new BlendShapeWeightAnimation(animation.Name, animation.Curve))
                .ToList()
        };

    public bool Equals(FacialBlendShapeData? other)
        => other is not null
        && ReferenceAnimations.SequenceEqual(other.ReferenceAnimations)
        && ClipAnimations.SequenceEqual(other.ClipAnimations)
        && BlendShapeAnimations.SequenceEqual(other.BlendShapeAnimations);

    public override bool Equals(object? obj)
        => obj is FacialBlendShapeData other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var reference in ReferenceAnimations)
            hash.Add(reference);
        foreach (var animation in ClipAnimations)
            hash.Add(animation);
        foreach (var animation in BlendShapeAnimations)
            hash.Add(animation);
        return hash.ToHashCode();
    }
}

internal enum ClipImportOption
{
    All = 0,
    NonZero = 10
}

[Serializable]
internal sealed class NonFacialAnimationData : IEquatable<NonFacialAnimationData>
{
    public List<Transform> ReferenceAnimations = new();
    public List<AnimationClip> AnimationClips = new();
    public List<TransformAnimation> TransformAnimations = new();

    internal NonFacialAnimationData Clone(Component owner)
        => new()
        {
            ReferenceAnimations = ReferenceAnimations.ToList(),
            AnimationClips = AnimationClips.ToList(),
            TransformAnimations = TransformAnimations
                .Where(animation => animation != null)
                .Select(animation => animation.Clone(owner))
                .ToList()
        };

    public bool Equals(NonFacialAnimationData? other)
        => other != null
        && ReferenceAnimations.SequenceEqual(other.ReferenceAnimations)
        && AnimationClips.SequenceEqual(other.AnimationClips)
        && TransformAnimations.SequenceEqual(other.TransformAnimations);

    public override bool Equals(object? obj)
        => obj is NonFacialAnimationData other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(ReferenceAnimations.Count, AnimationClips.Count, TransformAnimations.Count);
}

[Serializable]
internal sealed class TransformAnimation : IEquatable<TransformAnimation>
{
    public AvatarObjectReference Target = new();
    public AnimationCurve Curve = AnimationCurve.Constant(0f, 1f, 1f);

    internal TransformAnimation Clone(Component owner)
    {
        var curve = Curve ?? AnimationCurve.Constant(0f, 1f, 1f);
        return new TransformAnimation
        {
            Target = new AvatarObjectReference(Target.Get(owner)),
            Curve = new AnimationCurve(curve.keys)
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode
            }
        };
    }

    public bool Equals(TransformAnimation? other)
        => other != null
        && Target.Equals(other.Target)
        && Curve.preWrapMode == other.Curve.preWrapMode
        && Curve.postWrapMode == other.Curve.postWrapMode
        && Curve.keys.SequenceEqual(other.Curve.keys);

    public override bool Equals(object? obj)
        => obj is TransformAnimation other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Target, Curve);
}

/// <summary>platform標準Blinkか、FaceTune生成animationか。</summary>
[Serializable]
internal class EyeBlinkSettings : IEquatable<EyeBlinkSettings>
{
    public enum Kind
    {
        BuiltIn = 0,
        CustomAnimation = 10,
        SimpleAnimation = 20
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
            new Keyframe(0f, 0f),
            new Keyframe(.07f, 100f),
            new Keyframe(.14f, 0f)));

    public bool Equals(EyeBlinkSettings? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (EyeBlinkMode != other.EyeBlinkMode) return false;

        return EyeBlinkMode switch
        {
            Kind.BuiltIn => true,
            Kind.SimpleAnimation => IntervalSeconds == other.IntervalSeconds
                && SimpleDurationsSeconds == other.SimpleDurationsSeconds
                && SimpleBlinkBlendShapes.SequenceEqual(other.SimpleBlinkBlendShapes)
                && SimpleConflictPreventionBlendShapes.SequenceEqual(
                    other.SimpleConflictPreventionBlendShapes),
            Kind.CustomAnimation => IntervalSeconds == other.IntervalSeconds
                && Animations.SequenceEqual(other.Animations),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override bool Equals(object? obj)
        => obj is EyeBlinkSettings other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EyeBlinkMode);
        if (EyeBlinkMode != Kind.BuiltIn)
        {
            hash.Add(IntervalSeconds);
        }

        switch (EyeBlinkMode)
        {
            case Kind.SimpleAnimation:
                hash.Add(SimpleDurationsSeconds);
                AddSequenceHash(ref hash, SimpleBlinkBlendShapes);
                AddSequenceHash(ref hash, SimpleConflictPreventionBlendShapes);
                break;
            case Kind.CustomAnimation:
                AddSequenceHash(ref hash, Animations);
                break;
        }
        return hash.ToHashCode();
    }

    private static void AddSequenceHash<T>(ref HashCode hash, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            hash.Add(item);
        }
    }

    internal EyeBlinkSettings Clone()
        => new()
        {
            EyeBlinkMode = EyeBlinkMode,
            IntervalSeconds = IntervalSeconds,
            SimpleDurationsSeconds = SimpleDurationsSeconds,
            SimpleBlinkBlendShapes = SimpleBlinkBlendShapes.ToList(),
            SimpleConflictPreventionBlendShapes = SimpleConflictPreventionBlendShapes.ToList(),
            Animations = Animations
                .Select(animation => new BlendShapeWeightAnimation(animation.Name, animation.Curve))
                .ToList()
        };
}

/// <summary>LipSyncと競合するBlendShapeの打ち消し設定。</summary>
[Serializable]
internal class LipSyncSettings : IEquatable<LipSyncSettings>
{
    public List<BlendShapeWeight> CancellerBlendShapes = new();

    public bool Equals(LipSyncSettings? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CancellerBlendShapes.SequenceEqual(other.CancellerBlendShapes);
    }

    public override bool Equals(object? obj)
        => obj is LipSyncSettings other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var blendShape in CancellerBlendShapes)
        {
            hash.Add(blendShape);
        }
        return hash.ToHashCode();
    }

    internal LipSyncSettings Clone()
        => new() { CancellerBlendShapes = CancellerBlendShapes.ToList() };
}

/// <summary>表情の遷移時間。</summary>
[Serializable]
internal class TransitionSettings
{
    public float DurationSeconds = 0.1f;
}

/// <summary>
/// 表情の優先順位。同値ならHierarchy上で下が優先。
/// VRCではMergeAnimatorの優先度と同一。
/// </summary>
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
    Allow = 0,
    Disallow = 10,
    Keep = 20
}

internal enum ExpressionWriteMode
{
    Replace = 0,
    Blend = 10
}

/// <summary>表情animationの時間制御。</summary>
[Serializable]
internal class MultiFrameSettings : IEquatable<MultiFrameSettings>
{
    public enum Kind
    {
        Default = 0,
        Loop = 10,
        Trigger = 20,
        Parameter = 30,
        Menu = 40
    }

    public Kind MultiFrameMode = Kind.Default;

    public Hand TriggerHand = Hand.Left; // For Kind.Trigger
    public string ParameterName = string.Empty; // For Kind.Parameter
    public MenuComponent? MenuSource = null; // For Kind.Menu

    internal MultiFrameSettings Clone()
        => new()
        {
            MultiFrameMode = MultiFrameMode,
            TriggerHand = TriggerHand,
            ParameterName = ParameterName,
            MenuSource = MenuSource
        };

    public bool Equals(MultiFrameSettings? other)
        => other != null
        && MultiFrameMode == other.MultiFrameMode
        && TriggerHand == other.TriggerHand
        && ParameterName == other.ParameterName
        && MenuSource == other.MenuSource;

    public override bool Equals(object? obj)
        => obj is MultiFrameSettings other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(MultiFrameMode, TriggerHand, ParameterName, MenuSource);
}