namespace com.aoyon.facetune;

[Serializable]
public record GenericAnimation // Immutable
{
    [SerializeField] private SerializableCurveBinding _curveBinding;
    public SerializableCurveBinding CurveBinding { get => _curveBinding; init => _curveBinding = value; }

    [SerializeField] private AnimationCurve _curve;
    public AnimationCurve GetCurve() => _curve.Clone();

    [SerializeField] private List<SerializableObjectReferenceKeyframe> _objectReferenceCurve;
    public IReadOnlyList<SerializableObjectReferenceKeyframe> ObjectReferenceCurve { get => _objectReferenceCurve.AsReadOnly(); init => _objectReferenceCurve = value.ToList(); }

    public GenericAnimation()
    {
        _curveBinding = new SerializableCurveBinding();
        _curve = new AnimationCurve();
        _objectReferenceCurve = new List<SerializableObjectReferenceKeyframe>();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, AnimationCurve curve)
    {
        _curveBinding = curveBinding with {};
        _curve = curve.Clone();
        _objectReferenceCurve = new();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, IEnumerable<SerializableObjectReferenceKeyframe> objectReferenceCurve)
    {
        _curveBinding = curveBinding with {};
        _curve = new();
        _objectReferenceCurve = objectReferenceCurve.ToList();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, AnimationCurve curve, IEnumerable<SerializableObjectReferenceKeyframe> objectReferenceCurve)
    {
        _curveBinding = curveBinding with {};
        _curve = curve.Clone();
        _objectReferenceCurve = objectReferenceCurve.ToList();
    }

#if UNITY_EDITOR
    public static List<GenericAnimation> FromAnimationClip(AnimationClip clip)
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

            animations.Add(new GenericAnimation(serializableCurveBinding, curve, serializableObjectReferenceCurve));
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
        return HashCode.Combine(_curveBinding, _curve, _objectReferenceCurve);
    }
}
