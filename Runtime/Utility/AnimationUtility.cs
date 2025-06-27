namespace com.aoyon.facetune;

internal static class FTAnimationUtility
{
    // AnimationCurve
    public static AnimationCurve Clone(this AnimationCurve curve)
    {
        var duplicated = new AnimationCurve();
        duplicated.CopyFrom(curve);
        return duplicated;
    }

    private const string BlendShapePropertyName = FaceTuneConsts.AnimatedBlendShapePrefix;

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

    public static void GetBlendShapeAnimations(this AnimationClip clip, List<BlendShapeAnimation> resultToAdd, ClipExcludeOption? option = null, BlendShapeSet? defaultSet = null)
    {
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(BlendShapePropertyName)) continue;

            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null && curve.keys.Length > 0)
            {
                var name = binding.propertyName.Replace(BlendShapePropertyName, string.Empty);
                var weight = curve.Evaluate(0); // Todo: 他のフレームも見るべき？
                switch (option)
                {
                    case null:
                    case ClipExcludeOption.None:
                        resultToAdd.Add(new BlendShapeAnimation(name, curve));
                        break;
                    case ClipExcludeOption.ExcludeZeroWeight:
                        if (weight != 0)
                        {
                            resultToAdd.Add(new BlendShapeAnimation(name, curve));
                        }
                        break;
                    case ClipExcludeOption.ExcludeDefault:
                        if (defaultSet == null) throw new InvalidOperationException("defaultSet is null");
                        if (defaultSet.TryGetValue(name, out var defaultBlendShape) is false || defaultBlendShape.Weight != weight)
                        {
                            resultToAdd.Add(new BlendShapeAnimation(name, curve));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(option), option, null);
                }
            }
        }
    }

    public static void GetGenericAnimations(this AnimationClip clip, List<GenericAnimation> resultToAdd)
    {
        resultToAdd.AddRange(GenericAnimation.FromAnimationClip(clip));
    }

    public static void SetBlendShapes(this AnimationClip clip, string relativePath, IEnumerable<BlendShape> blendShapes)
    {
        foreach (var blendShape in blendShapes)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0, blendShape.Weight);
            var binding = UnityEditor.EditorCurveBinding.FloatCurve(relativePath, typeof(SkinnedMeshRenderer), BlendShapePropertyName + blendShape.Name);
            UnityEditor.AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }

    public static void SetGenericAnimations(this AnimationClip clip, IEnumerable<GenericAnimation> genericAnimations)
    {
        foreach (var genericAnimation in genericAnimations)
        {
            var binding = genericAnimation.CurveBinding.ToEditorCurveBinding();
            UnityEditor.AnimationUtility.SetEditorCurve(clip, binding, genericAnimation.Curve);
        }
    }
#endif
}