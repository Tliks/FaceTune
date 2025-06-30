namespace aoyon.facetune;

[Serializable]
public record GenericAnimation // Immutable
{
    [SerializeField] private SerializableCurveBinding curveBinding;
    public SerializableCurveBinding CurveBinding { get => curveBinding; init => curveBinding = value; }
    public const string CurveBindingPropName = nameof(curveBinding);

    [SerializeField] private AnimationCurve curve; // AnimationCurveは可変
    public AnimationCurve Curve { get => curve.Clone(); init => curve = value.Clone(); }
    public const string CurvePropName = nameof(curve);

    [SerializeField] private List<SerializableObjectReferenceKeyframe> objectReferenceCurve;
    public IReadOnlyList<SerializableObjectReferenceKeyframe> ObjectReferenceCurve { get => objectReferenceCurve.AsReadOnly(); init => objectReferenceCurve = value.ToList(); }
    public const string ObjectReferenceCurvePropName = nameof(objectReferenceCurve);

    public GenericAnimation()
    {
        curveBinding = new SerializableCurveBinding();
        curve = new AnimationCurve();
        objectReferenceCurve = new List<SerializableObjectReferenceKeyframe>();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, AnimationCurve curve)
    {
        this.curveBinding = curveBinding;
        this.curve = curve.Clone();
        objectReferenceCurve = new List<SerializableObjectReferenceKeyframe>();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, IReadOnlyList<SerializableObjectReferenceKeyframe> objectReferenceCurve)
    {
        this.curveBinding = curveBinding;
        curve = new();
        this.objectReferenceCurve = objectReferenceCurve.ToList();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, AnimationCurve curve, IReadOnlyList<SerializableObjectReferenceKeyframe> objectReferenceCurve)
    {
        this.curveBinding = curveBinding;
        this.curve = curve.Clone();
        this.objectReferenceCurve = objectReferenceCurve.ToList();
    }

    internal float Time => curve.keys.Max(k => k.time);

    internal GenericAnimation ToSingleFrame()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, this.curve.Evaluate(0));
        var objectReferenceCurve = new[] { this.objectReferenceCurve.OrderBy(k => k.Time).FirstOrDefault() };
        return new GenericAnimation(curveBinding, curve, objectReferenceCurve);
    }

    private static readonly string BlendShapePrefix = FaceTuneConsts.AnimatedBlendShapePrefix;
    internal bool IsBlendShapeAnimation()
    {
        return curveBinding.Type == typeof(SkinnedMeshRenderer) && curveBinding.PropertyName.StartsWith(BlendShapePrefix);
    }
    internal bool TryToBlendShapeAnimation([NotNullWhen(true)] out BlendShapeAnimation? animation)
    {
        if (IsBlendShapeAnimation())
        {
            var name = curveBinding.PropertyName.Substring(BlendShapePrefix.Length);
            animation = new BlendShapeAnimation(name, curve);
            return true;
        }
        animation = null;
        return false;
    }

#if UNITY_EDITOR
    internal static List<GenericAnimation> FromAnimationClip(AnimationClip clip)
    {
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        var animations = new List<GenericAnimation>();
        foreach (var binding in bindings)
        {
            var serializableCurveBinding = SerializableCurveBinding.FromEditorCurveBinding(binding);

            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            var objectReferenceCurve = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);

            curve ??= new AnimationCurve();
            objectReferenceCurve ??= new UnityEditor.ObjectReferenceKeyframe[0];
            var serializableObjectReferenceCurve = objectReferenceCurve.Select(SerializableObjectReferenceKeyframe.FromEditorObjectReferenceKeyframe);

            animations.Add(new GenericAnimation(serializableCurveBinding, curve, serializableObjectReferenceCurve.ToList()));
        }
        return animations;
    }
#endif

    public virtual bool Equals(GenericAnimation other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return curveBinding.Equals(other.curveBinding)
            && curve.Equals(other.curve)
            && objectReferenceCurve.SequenceEqual(other.objectReferenceCurve);
    }

    public override int GetHashCode()
    {
        var hash = curveBinding.GetHashCode();
        hash ^= curve.GetHashCode();
        foreach (var keyframe in objectReferenceCurve)
        {
            hash ^= keyframe.GetHashCode();
        }
        return hash;
    }
}
