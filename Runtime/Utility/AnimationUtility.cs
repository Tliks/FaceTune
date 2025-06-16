namespace com.aoyon.facetune;

internal static class FTAnimationUtility
{
    private static readonly string BlendShapePrefix = "blendShape.";

    // AnimationClip
#if UNITY_EDITOR
    public static Dictionary<string, Dictionary<string, AnimationCurve>> GetBlendShapeCurves(this AnimationClip clip, bool clone)
    {
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        var curves = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        foreach (var binding in bindings)
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(BlendShapePrefix)) continue;

            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null) continue;

            var name = binding.propertyName.Replace(BlendShapePrefix, string.Empty);
            var duplicatedCurve = clone ? curve.Clone() : curve;

            var pathCurves = curves.GetOrAddNew(binding.path);
            pathCurves[name] = duplicatedCurve;
        }
        return curves;
    }
#endif


    // AnimationCurve
    public static AnimationCurve Clone(this AnimationCurve curve)
    {
        var duplicated = new AnimationCurve();
        duplicated.CopyFrom(curve);
        return duplicated;
    }
}