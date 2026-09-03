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

    public FaceTuneTagComponent? ComponentSource;
}

/// <summary>
/// 顔のBlendShape data。Simpleはbase sourceの後にlocalを重ね、Compositeはentry順に重ねる。
/// 後の同名BlendShapeが前を置き換え、0も明示値として扱う。
/// </summary>
[Serializable]
internal class FacialBlendShapeData : IEquatable<FacialBlendShapeData>
{
    public enum Mode
    {
        Simple = 10,
        Composite = 20
    }

    public Mode BlendShapeMode = Mode.Simple;

#region Simple Mode

    public enum SimpleBaseSource
    {
        Clip = 10,
        Reference = 20
    }

    public SimpleBaseSource BaseSource = SimpleBaseSource.Clip;

    // SimpleBaseSource.Clip
    public AnimationClip? Clip = null;
    public ClipImportOption ClipOption = ClipImportOption.NonZero;
    // SimpleBaseSource.Reference
    public FaceTuneTagComponent? ReferenceSource = null;

    // Simpleモード共通
    public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

#endregion

#region Composite Mode

    [Serializable]
    public class CompositeEntry
    {
        public enum Kind
        {
            Direct = 10,
            Clip = 20,
            Reference = 30
        }

        public Kind EntryKind = Kind.Clip;

        // Direct
        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();
        // Clip
        public AnimationClip? Clip = null;
        public ClipImportOption ClipOption = ClipImportOption.NonZero;
        // Reference
        public FaceTuneTagComponent? ReferenceSource = null;
    }

    public List<CompositeEntry> CompositeEntries = new();

#endregion

    internal FacialBlendShapeData Clone()
        => new()
        {
            BlendShapeMode = BlendShapeMode,
            BaseSource = BaseSource,
            Clip = Clip,
            ClipOption = ClipOption,
            ReferenceSource = ReferenceSource,
            BlendShapeAnimations = CloneAnimations(BlendShapeAnimations),
            CompositeEntries = CompositeEntries.Select(CloneEntry).ToList()
        };

    public bool Equals(FacialBlendShapeData? other)
        => other is not null
        && BlendShapeMode == other.BlendShapeMode
        && BaseSource == other.BaseSource
        && Clip == other.Clip
        && ClipOption == other.ClipOption
        && ReferenceSource == other.ReferenceSource
        && BlendShapeAnimations.SequenceEqual(other.BlendShapeAnimations)
        && CompositeEntries.Count == other.CompositeEntries.Count
        && CompositeEntries.Zip(other.CompositeEntries, EntryEquals).All(equal => equal);

    public override bool Equals(object? obj)
        => obj is FacialBlendShapeData other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BlendShapeMode);
        hash.Add(BaseSource);
        hash.Add(Clip);
        hash.Add(ClipOption);
        hash.Add(ReferenceSource);
        foreach (var animation in BlendShapeAnimations) hash.Add(animation);
        foreach (var entry in CompositeEntries)
        {
            hash.Add(entry.EntryKind);
            hash.Add(entry.Clip);
            hash.Add(entry.ClipOption);
            hash.Add(entry.ReferenceSource);
            foreach (var animation in entry.BlendShapeAnimations) hash.Add(animation);
        }
        return hash.ToHashCode();
    }

    private static CompositeEntry CloneEntry(CompositeEntry entry)
        => new()
        {
            EntryKind = entry.EntryKind,
            BlendShapeAnimations = CloneAnimations(entry.BlendShapeAnimations),
            Clip = entry.Clip,
            ClipOption = entry.ClipOption,
            ReferenceSource = entry.ReferenceSource
        };

    private static List<BlendShapeWeightAnimation> CloneAnimations(
        IEnumerable<BlendShapeWeightAnimation> animations)
        => animations.Select(animation =>
            new BlendShapeWeightAnimation(animation.Name, animation.Curve)).ToList();

    private static bool EntryEquals(CompositeEntry left, CompositeEntry right)
        => left.EntryKind == right.EntryKind
        && left.Clip == right.Clip
        && left.ClipOption == right.ClipOption
        && left.ReferenceSource == right.ReferenceSource
        && left.BlendShapeAnimations.SequenceEqual(right.BlendShapeAnimations);
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