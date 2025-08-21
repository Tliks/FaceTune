namespace Aoyon.FaceTune;

internal static class AnimationUtility
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

    public static void GetFirstFrameBlendShapes(this AnimationClip clip, ICollection<BlendShapeWeight> resultToAdd, ClipImportOption option, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, string? facialPath = null)
    {
        ProcessBlendShapeBindings(clip, option, facialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeight(name, curve.Evaluate(0))), facialPath);
    }

    public static void GetAllFirstFrameBlendShapes(this AnimationClip clip, ICollection<BlendShapeWeight> resultToAdd, string? facialPath = null)
    {
        ProcessBlendShapeBindings(clip, ClipImportOption.All, _emptyFacialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeight(name, curve.Evaluate(0))), facialPath);
    }

    public static void GetNonZeroBlendShapes(this AnimationClip clip, ICollection<BlendShapeWeight> resultToAdd, string? facialPath = null)
    {
        ProcessBlendShapeBindings(clip, ClipImportOption.NonZero, _emptyFacialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeight(name, curve.Evaluate(0))), facialPath);
    }

    public static void GetBlendShapeAnimations(this AnimationClip clip, ICollection<BlendShapeWeightAnimation> resultToAdd, ClipImportOption option, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, string? facialPath = null)
    {
        ProcessBlendShapeBindings(clip, option, facialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeightAnimation(name, curve)), facialPath);
    }

    public static void GetAllBlendShapeAnimations(this AnimationClip clip, ICollection<BlendShapeWeightAnimation> resultToAdd, string? facialPath = null)
    {
        ProcessBlendShapeBindings(clip, ClipImportOption.All, _emptyFacialAnimations, (name, curve) => resultToAdd.Add(new BlendShapeWeightAnimation(name, curve)), facialPath);
    }

    private static void ProcessBlendShapeBindings(this AnimationClip clip, ClipImportOption option, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, Action<string, AnimationCurve> addAction, string? facialPath = null)
    {
        var facialStyleCurves = facialAnimations.ToDictionary(a => a.Name, a => a.Curve);
        
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (!IsFacialBinding(binding, facialPath)) continue;

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
                        if (facialStyleCurves.TryGetValue(name, out var facialCurve))
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

    public static void ProcessAllBindings(this AnimationClip clip, ClipImportOption option, IReadOnlyList<BlendShapeWeightAnimation> facialstyle, List<BlendShapeWeightAnimation> facialAnimations, List<GenericAnimation> nonFacialAnimations, string? facialPath = null)
    {
        var facialStyleCurves = facialstyle.ToDictionary(a => a.Name, a => a.Curve);

        var curveBindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in curveBindings)
        {
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null && curve.keys.Length > 0)
            {
                if (IsFacialBinding(binding, facialPath))
                {
                    var name = binding.propertyName.Replace(BlendShapePropertyName, string.Empty);
                    var isZero = curve.keys.All(k => k.value == 0);
                    switch (option)
                    {
                        case ClipImportOption.All:
                            facialAnimations.Add(new BlendShapeWeightAnimation(name, curve));
                            break;
                        case ClipImportOption.NonZero:
                            if (!isZero)
                            {
                                facialAnimations.Add(new BlendShapeWeightAnimation(name, curve));
                            }
                            break;
                        case ClipImportOption.FacialStyleOverridesOrNonZero:
                            if (facialStyleCurves.TryGetValue(name, out var facialCurve))
                            {
                                if (!facialCurve.Equals(curve))
                                {
                                    facialAnimations.Add(new BlendShapeWeightAnimation(name, curve)); // override
                                }
                                break;
                            }
                            else
                            {
                                if (!isZero)
                                {
                                    facialAnimations.Add(new BlendShapeWeightAnimation(name, curve));
                                }
                                break;
                            }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(option), option, null);
                    }
                }
                else
                {
                    var serializableCurveBinding = SerializableCurveBinding.FromEditorCurveBinding(binding);
                    nonFacialAnimations.Add(new GenericAnimation(serializableCurveBinding, curve));
                }
            }
        }

        var objectReferenceBindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (var binding in objectReferenceBindings)
        {
            var serializableCurveBinding = SerializableCurveBinding.FromEditorCurveBinding(binding);
            var objectReferenceCurve = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);
            var serializableObjectReferenceCurve = objectReferenceCurve.Select(SerializableObjectReferenceKeyframe.FromEditorObjectReferenceKeyframe);
            nonFacialAnimations.Add(new GenericAnimation(serializableCurveBinding, serializableObjectReferenceCurve.ToList()));
        }
    }


    private static bool IsFacialBinding(UnityEditor.EditorCurveBinding binding, string? facialPath)
    {
        if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith(BlendShapePropertyName))
        {
            if (facialPath != null)
            {
                return binding.path.ToLower() == facialPath.ToLower();
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    public static void GetGenericAnimations(this AnimationClip clip, ICollection<GenericAnimation> resultToAdd)
    {
        foreach (var animation in GenericAnimation.FromAnimationClip(clip))
        {
            resultToAdd.Add(animation);
        }
    }

    public static void AddBlendShapes(this AnimationClip clip, string relativePath, IEnumerable<BlendShapeWeight> blendShapes)
    {
        var bindings = new List<UnityEditor.EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        foreach (var blendShape in blendShapes)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0, blendShape.Weight);
            var binding = UnityEditor.EditorCurveBinding.FloatCurve(relativePath, typeof(SkinnedMeshRenderer), BlendShapePropertyName + blendShape.Name);
            bindings.Add(binding);
            curves.Add(curve);
        }
        UnityEditor.AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
    }

    public static void AddGenericAnimations(this AnimationClip clip, IEnumerable<GenericAnimation> genericAnimations)
    {
        var bindings = new List<UnityEditor.EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        foreach (var genericAnimation in genericAnimations)
        {
            var binding = genericAnimation.CurveBinding.ToEditorCurveBinding();
            bindings.Add(binding);
            curves.Add(genericAnimation.Curve);
        }
        UnityEditor.AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
    }

    public static void RemoveAllCurveBindings(this AnimationClip clip)
    {
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        var curves = Enumerable.Repeat<AnimationCurve?>(null, bindings.Length).ToArray();
        UnityEditor.AnimationUtility.SetEditorCurves(clip, bindings, curves);
    }

    public static void SaveChanges(this AnimationClip clip)
    {
        UnityEditor.EditorUtility.SetDirty(clip);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(clip);
    }
#endif
}