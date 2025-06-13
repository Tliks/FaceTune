namespace com.aoyon.facetune;

[Serializable]
public record GenericAnimation // Immutable
{
    [SerializeField] private SerializableCurveBinding _curveBinding;
    public SerializableCurveBinding CurveBinding { get => _curveBinding; init => _curveBinding = value; }

    [SerializeField] private AnimationCurve _curve;
    public AnimationCurve GetCurve() => _curve.Clone();

    public GenericAnimation()
    {
        _curveBinding = new SerializableCurveBinding();
        _curve = new AnimationCurve();
    }

    public GenericAnimation(SerializableCurveBinding curveBinding, AnimationCurve curve)
    {
        _curveBinding = curveBinding with {};
        _curve = curve.Clone();
    }

#if UNITY_EDITOR
    public static List<GenericAnimation> FromAnimationClip(AnimationClip clip)
    {
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        var animations = new List<GenericAnimation>();
        foreach (var binding in bindings)
        {
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null)
            animations.Add(new GenericAnimation(SerializableCurveBinding.FromEditorCurveBinding(binding), curve));
        }
        return animations;
    }
#endif
}