namespace Aoyon.FaceTune;

/// <summary>Avatar-relative animation curves with facial renderer blend shapes removed.</summary>
internal sealed class ResolvedNonFacialAnimationSet : IEquatable<ResolvedNonFacialAnimationSet>
{
    private readonly Dictionary<EditorCurveBinding, AnimationCurve> _floatCurves = new();
    private readonly Dictionary<EditorCurveBinding, ObjectReferenceKeyframe[]> _objectCurves = new();

    public IEnumerable<KeyValuePair<EditorCurveBinding, AnimationCurve>> FloatCurves => _floatCurves;
    public IEnumerable<KeyValuePair<EditorCurveBinding, ObjectReferenceKeyframe[]>> ObjectCurves => _objectCurves;

    public bool IsTimeDependent
        => _floatCurves.Values.Any(curve => curve != null && curve.keys.Any(key => key.time != 0f))
        || _objectCurves.Values.Any(curve => curve.Any(key => key.time != 0f));

    public void AddFloatCurve(EditorCurveBinding binding, AnimationCurve curve)
        => _floatCurves[binding] = curve;

    public void AddObjectCurve(EditorCurveBinding binding, ObjectReferenceKeyframe[] curve)
        => _objectCurves[binding] = curve;

    public bool Equals(ResolvedNonFacialAnimationSet? other)
        => other != null
        && CurvesEqual(_floatCurves, other._floatCurves, (left, right) => ReferenceEquals(left, right))
        && CurvesEqual(_objectCurves, other._objectCurves, (left, right) => left.SequenceEqual(right));

    public override bool Equals(object? obj) => obj is ResolvedNonFacialAnimationSet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = _floatCurves.Count ^ _objectCurves.Count;
        foreach (var (binding, curve) in _floatCurves)
            hash ^= HashCode.Combine(binding, curve);
        foreach (var (binding, curve) in _objectCurves)
        {
            var curveHash = new HashCode();
            foreach (var keyframe in curve) curveHash.Add(keyframe);
            hash ^= HashCode.Combine(binding, curveHash.ToHashCode());
        }
        return hash;
    }

    private static bool CurvesEqual<TCurve>(
        IReadOnlyDictionary<EditorCurveBinding, TCurve> left,
        IReadOnlyDictionary<EditorCurveBinding, TCurve> right,
        Func<TCurve, TCurve, bool> equals)
    {
        if (left.Count != right.Count) return false;
        return left.All(entry => right.TryGetValue(entry.Key, out var value)
            && equals(entry.Value, value));
    }
}
