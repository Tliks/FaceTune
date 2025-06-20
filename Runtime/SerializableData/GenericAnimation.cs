namespace com.aoyon.facetune;

[Serializable]
public record GenericAnimation // Immutable
{
    [SerializeField] private SerializableCurveBinding _curveBinding;
    public const string CurveBindingPropName = "_curveBinding";
    public SerializableCurveBinding CurveBinding { get => _curveBinding; init => _curveBinding = value; }

    [SerializeField] private AnimationCurve _curve; // AnimationCurveは可変
    public const string CurvePropName = "_curve";
    public AnimationCurve Curve { get => _curve.Clone(); init => _curve = value.Clone(); }

    [SerializeField] private List<SerializableObjectReferenceKeyframe> _objectReferenceCurve;
    public const string ObjectReferenceCurvePropName = "_objectReferenceCurve";
    public IReadOnlyList<SerializableObjectReferenceKeyframe> ObjectReferenceCurve { get => _objectReferenceCurve.AsReadOnly(); init => _objectReferenceCurve = value.ToList(); }

    public GenericAnimation()
    {
        _curveBinding = new SerializableCurveBinding();
        _curve = new AnimationCurve();
        _objectReferenceCurve = new List<SerializableObjectReferenceKeyframe>();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, AnimationCurve curve)
    {
        _curveBinding = curveBinding;
        _curve = curve.Clone();
        _objectReferenceCurve = new List<SerializableObjectReferenceKeyframe>();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, IReadOnlyList<SerializableObjectReferenceKeyframe> objectReferenceCurve)
    {
        _curveBinding = curveBinding;
        _curve = new();
        _objectReferenceCurve = objectReferenceCurve.ToList();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, AnimationCurve curve, IReadOnlyList<SerializableObjectReferenceKeyframe> objectReferenceCurve)
    {
        _curveBinding = curveBinding;
        _curve = curve.Clone();
        _objectReferenceCurve = objectReferenceCurve.ToList();
    }

    internal GenericAnimation ToSingleFrame()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0, _curve.Evaluate(0));
        var objectReferenceCurve = _objectReferenceCurve.OrderBy(k => k.Time).FirstOrDefault();
        return new GenericAnimation(_curveBinding, curve, new[] { objectReferenceCurve });
    }

    private static readonly string BlendShapePrefix = "blendShape.";
    internal bool IsBlendShapeAnimation()
    {
        return _curveBinding.Type == typeof(SkinnedMeshRenderer) && _curveBinding.PropertyName.StartsWith(BlendShapePrefix);
    }
    internal bool TryToBlendShapeAnimation([NotNullWhen(true)] out BlendShapeAnimation? animation)
    {
        if (IsBlendShapeAnimation())
        {
            var name = _curveBinding.PropertyName.Substring(BlendShapePrefix.Length);
            animation = new BlendShapeAnimation(name, _curve.Clone());
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
        return _curveBinding.Equals(other._curveBinding)
            && _curve.Equals(other._curve)
            && _objectReferenceCurve.SequenceEqual(other._objectReferenceCurve);
    }

    public override int GetHashCode()
    {
        var hash = _curveBinding.GetHashCode();
        hash ^= _curve.GetHashCode();
        foreach (var keyframe in _objectReferenceCurve)
        {
            hash ^= keyframe.GetHashCode();
        }
        return hash;
    }
}
