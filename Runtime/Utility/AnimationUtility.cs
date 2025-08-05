namespace Aoyon.FaceTune;

internal static class FTAnimationUtility
{
    // AnimationCurve
    public static AnimationCurve Clone(this AnimationCurve curve)
    {
        var duplicated = new AnimationCurve();
        duplicated.CopyFrom(curve);
        return duplicated;
    }

    private const string BlendShapePropertyName = FaceTuneConstants.AnimatedBlendShapePrefix;

#if UNITY_EDITOR

    private static readonly List<BlendShapeWeightAnimation> _emptyFacialAnimations = new();

    public static void GetFirstFrameBlendShapes(this AnimationClip clip, ICollection<BlendShapeWeight> resultToAdd, ClipImportOption option, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations)
    {
        ProcessBlendShapeBindings(clip, option, facialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeight(name, curve.Evaluate(0))));
    }

    public static void GetAllFirstFrameBlendShapes(this AnimationClip clip, ICollection<BlendShapeWeight> resultToAdd)
    {
        ProcessBlendShapeBindings(clip, ClipImportOption.All, _emptyFacialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeight(name, curve.Evaluate(0))));
    }

    public static void GetNonZeroBlendShapes(this AnimationClip clip, ICollection<BlendShapeWeight> resultToAdd)
    {
        ProcessBlendShapeBindings(clip, ClipImportOption.NonZero, _emptyFacialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeight(name, curve.Evaluate(0))));
    }

    public static void GetBlendShapeAnimations(this AnimationClip clip, ICollection<BlendShapeWeightAnimation> resultToAdd, ClipImportOption option, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations)
    {
        ProcessBlendShapeBindings(clip, option, facialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeightAnimation(name, curve)));
    }

    public static void GetAllBlendShapeAnimations(this AnimationClip clip, ICollection<BlendShapeWeightAnimation> resultToAdd)
    {
        ProcessBlendShapeBindings(clip, ClipImportOption.All, _emptyFacialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeightAnimation(name, curve)));
    }

    private static void ProcessBlendShapeBindings(this AnimationClip clip, ClipImportOption option, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, Action<string, AnimationCurve> addAction)
    {
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        var facialCurves = facialAnimations.ToDictionary(a => a.Name, a => a.Curve);
        foreach (var binding in bindings)
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(BlendShapePropertyName)) continue;

            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null && curve.keys.Length > 0)
            {
                var add = false;
                var name = binding.propertyName.Replace(BlendShapePropertyName, string.Empty);
                var isZero = curve.keys.All(k => k.value == 0);
                switch (option)
                {
                    case ClipImportOption.All:
                        add = true;
                        break;
                    case ClipImportOption.NonZero:
                        if (!isZero)
                        {
                            add = true;
                        }
                        break;
                    case ClipImportOption.FacialStyleOverridesOrNonZero:
                        if (facialCurves.TryGetValue(name, out var facialCurve))
                        {
                            if (!facialCurve.Equals(curve))
                            {
                                add = true; // override
                            }
                            break;
                        }
                        else
                        {
                            if (!isZero)
                            {
                                add = true;
                            }
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(option), option, null);
                }
                if (add)
                {
                    addAction(name, curve);
                }
            }
        }
    }

    public static void GetGenericAnimations(this AnimationClip clip, List<GenericAnimation> resultToAdd)
    {
        resultToAdd.AddRange(GenericAnimation.FromAnimationClip(clip));
    }

    public static void SetBlendShapes(this AnimationClip clip, string relativePath, IEnumerable<BlendShapeWeight> blendShapes)
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