namespace com.aoyon.facetune;

internal static class AnimationUtility
{
    public static List<BlendShape> GetBlendShapesFromClip(AnimationClip clip, bool first = true)
    {
        var blendShapes = new List<BlendShape>();
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith("blendShape.")) continue;

            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null && curve.keys.Length > 0)
            {
                var name = binding.propertyName.Replace("blendShape.", string.Empty);
                var weight = first ? curve.keys[0].value : curve.keys[curve.keys.Length - 1].value;
                blendShapes.Add(new BlendShape(name, weight));
            }
        }
        return blendShapes;
    }

    // Todo: 多分動いていない
    public static void ClearCurveBindings(AnimationClip clip)
    {
        clip.ClearCurves();
    }

    public static void SetBlendShapesToClip(AnimationClip clip, string relativePath, IEnumerable<BlendShape> blendShapes)
    {
        foreach (var blendShape in blendShapes)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0, blendShape.Weight);
            var binding = EditorCurveBinding.FloatCurve(relativePath, typeof(SkinnedMeshRenderer), "blendShape." + blendShape.Name);
            UnityEditor.AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }
}

