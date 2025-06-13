namespace com.aoyon.facetune;

// 遅いのでビルドではVirtualAnimationUtilityを使うように
internal static class AnimationUtility
{
    public static List<BlendShape> GetBlendShapes(this AnimationClip clip, bool first = true)
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
    
    public static void SetBlendShapes(this AnimationClip clip, string relativePath, IEnumerable<BlendShape> blendShapes)
    {
        foreach (var blendShape in blendShapes)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0, blendShape.Weight);
            var binding = EditorCurveBinding.FloatCurve(relativePath, typeof(SkinnedMeshRenderer), "blendShape." + blendShape.Name);
            UnityEditor.AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }

    public static void SetGenericAnimations(this AnimationClip clip, IEnumerable<GenericAnimation> genericAnimations)
    {
        foreach (var genericAnimation in genericAnimations)
        {
            var binding = genericAnimation.CurveBinding.ToEditorCurveBinding();
            UnityEditor.AnimationUtility.SetEditorCurve(clip, binding, genericAnimation.GetCurve());
        }
    }
}