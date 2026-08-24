namespace Aoyon.FaceTune;

internal static partial class Utils
{
    private const string BlendShapePropertyName = "blendShape.";

    public static void GetFirstFrameBlendShapes(this AnimationClip clip, ClipImportOption option, ICollection<BlendShapeWeight> resultToAdd, string facialPath)
    {
        ProcessBlendShapeBindings(clip, option, (name, curve) => resultToAdd.Add(new BlendShapeWeight(name, curve.Evaluate(0))), facialPath);
    }
    
    public static void GetBlendShapeAnimations(this AnimationClip clip, ClipImportOption option, ICollection<BlendShapeWeightAnimation> resultToAdd, string facialPath)
    {
        ProcessBlendShapeBindings(clip, option, (name, curve) => resultToAdd.Add(new BlendShapeWeightAnimation(name, curve)), facialPath);
    }

    private static void ProcessBlendShapeBindings(this AnimationClip clip, ClipImportOption option, Action<string, AnimationCurve> addAction, string facialPath)
    {
        if (clip == null) return;
        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (!IsFacialBinding(binding, facialPath)) continue;

            var curve = AnimationUtility.GetEditorCurve(clip, binding);
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


    private static bool IsFacialBinding(EditorCurveBinding binding, string facialPath)
    {
        if (binding.type != typeof(SkinnedMeshRenderer)
            || !binding.propertyName.StartsWith(BlendShapePropertyName)) return false;
        return string.IsNullOrEmpty(facialPath)
            || string.Equals(binding.path, facialPath, StringComparison.OrdinalIgnoreCase);
    }

    public static void AddBlendShapes(this AnimationClip clip, string relativePath, IEnumerable<BlendShapeWeight> blendShapes)
    {
        var bindings = new List<EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        foreach (var blendShape in blendShapes)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0, blendShape.Weight);
            var binding = EditorCurveBinding.FloatCurve(relativePath, typeof(SkinnedMeshRenderer), BlendShapePropertyName + blendShape.Name);
            bindings.Add(binding);
            curves.Add(curve);
        }
        AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
    }

    public static void AddBlendShapeAnimations(this AnimationClip clip, string relativePath, IEnumerable<BlendShapeWeightAnimation> animations)
    {
        var bindings = new List<EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        foreach (var animation in animations)
        {
            var binding = EditorCurveBinding.FloatCurve(relativePath, typeof(SkinnedMeshRenderer), BlendShapePropertyName + animation.Name);
            bindings.Add(binding);
            curves.Add(animation.Curve);
        }
        AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
    }

    public static void SaveChanges(this AnimationClip clip)
    {
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
    }
}