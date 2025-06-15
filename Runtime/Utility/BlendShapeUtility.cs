namespace com.aoyon.facetune;

internal static class BlendShapeUtility
{
    public static BlendShape[] GetBlendShapes(this SkinnedMeshRenderer renderer, Mesh mesh)
    {
        var blendShapes = new BlendShape[mesh.blendShapeCount];
        for (var i = 0; i < mesh.blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            var weight = renderer.GetBlendShapeWeight(i);
            blendShapes[i] = new BlendShape(name, weight);
        }
        return blendShapes;
    }

    public static BlendShapeSet ToSet(this IEnumerable<BlendShape> blendShapes)
    {
        return new BlendShapeSet(blendShapes);
    }

#if UNITY_EDITOR
    public static BlendShapeSet GetBlendShapeSetFromClip(AnimationClip clip, ClipExcludeOption clipExcludeOption, BlendShapeSet defaultSet)
    {
        var blendShapes = new BlendShapeSet(GetBlendShapesFromClip(clip));
        switch (clipExcludeOption)
        {
            case ClipExcludeOption.None:
                break;
            case ClipExcludeOption.ExcludeZeroWeight:
                blendShapes.RemoveZeroWeight();
                break;
            case ClipExcludeOption.ExcludeDefault:
                blendShapes = blendShapes.Except(defaultSet);
                break;
        }
        return blendShapes;
    }

    // AnimationUtilityからコピー
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
#endif
}
