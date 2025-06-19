namespace com.aoyon.facetune;

internal static class BlendShapeUtility
{
    private const string BlendShapePropertyName = "blendShape.";

    public static void GetBlendShapes(this SkinnedMeshRenderer renderer, ICollection<BlendShape> resultToAdd)
    {
        for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
        {
            var name = renderer.sharedMesh.GetBlendShapeName(i);
            var weight = renderer.GetBlendShapeWeight(i);
            resultToAdd.Add(new BlendShape(name, weight));
        }
    }

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
    
#if UNITY_EDITOR
    public static void GetFirstFrameBlendShapes(this AnimationClip clip, ICollection<BlendShape> resultToAdd, ClipExcludeOption? option = null, BlendShapeSet? defaultSet = null)
    {
        GetBlendShapes(clip, 0, resultToAdd, option, defaultSet);
    }

    public static void GetBlendShapes(this AnimationClip clip, float time, ICollection<BlendShape> resultToAdd, ClipExcludeOption? option = null, BlendShapeSet? defaultSet = null)
    {
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(BlendShapePropertyName)) continue;

            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null && curve.keys.Length > 0)
            {
                var name = binding.propertyName.Replace(BlendShapePropertyName, string.Empty);
                var weight = curve.Evaluate(time);
                switch (option)
                {
                    case null:
                    case ClipExcludeOption.None:
                        resultToAdd.Add(new BlendShape(name, weight));
                        break;
                    case ClipExcludeOption.ExcludeZeroWeight:
                        if (weight != 0)
                        {
                            resultToAdd.Add(new BlendShape(name, weight));
                        }
                        break;
                    case ClipExcludeOption.ExcludeDefault:
                        if (defaultSet == null) throw new InvalidOperationException("defaultSet is null");
                        if (defaultSet.TryGetValue(name, out var defaultBlendShape) is false || defaultBlendShape.Weight != weight)
                        {
                            resultToAdd.Add(new BlendShape(name, weight));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(option), option, null);
                }
            }
        }
    }
#endif
}