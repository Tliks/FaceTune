namespace aoyon.facetune;

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
    public static void GetFirstFrameBlendShapes(this AnimationClip clip, ICollection<BlendShape> resultToAdd, ClipImportOption? option = null, BlendShapeSet? facialStyleSet = null)
    {
        GetBlendShapes(clip, 0, resultToAdd, option, facialStyleSet);
    }

    private static void ProcessBlendShapeBindings<T>(
        AnimationClip clip,
        float time,
        ClipImportOption? option,
        BlendShapeSet? facialStyleSet,
        Action<string, float, AnimationCurve> process)
    {
        if (option == ClipImportOption.FacialStyleOverridesOrNonZero && facialStyleSet == null)
            throw new InvalidOperationException("facialStyleSet is null");

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
                    case ClipImportOption.All:
                        process(name, weight, curve);
                        break;
                    case ClipImportOption.NonZero:
                        if (weight != 0)
                        {
                            process(name, weight, curve);
                        }
                        break;
                    case ClipImportOption.FacialStyleOverridesOrNonZero:
                        if (facialStyleSet!.TryGetValue(name, out var facialStyle))
                        {
                            if (facialStyle.Weight == weight)
                            {
                                continue;
                            }
                            else
                            {
                                process(name, weight, curve);
                                continue;
                            }
                        }
                        else
                        {
                            if (weight == 0)
                            {
                                continue;
                            }
                            else
                            {
                                process(name, weight, curve);
                                continue;
                            }
                        }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(option), option, null);
                }
            }
        }
    }

    public static void GetBlendShapes(this AnimationClip clip, float time, ICollection<BlendShape> resultToAdd, ClipImportOption? option = null, BlendShapeSet? facialStyleSet = null)
    {
        ProcessBlendShapeBindings<BlendShape>(
            clip,
            time,
            option,
            facialStyleSet,
            (name, weight, curve) => resultToAdd.Add(new BlendShape(name, weight))
        );
    }

    public static void GetBlendShapeAnimations(this AnimationClip clip, List<BlendShapeAnimation> resultToAdd, ClipImportOption? option = null, BlendShapeSet? facialStyleSet = null)
    {
        // 比較用のフレームはアニメーションは常に0フレーム目を参照
        ProcessBlendShapeBindings<BlendShapeAnimation>(
            clip,
            0,
            option,
            facialStyleSet,
            (name, weight, curve) => resultToAdd.Add(new BlendShapeAnimation(name, curve))
        );
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