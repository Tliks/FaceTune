using nadena.dev.ndmf.animator;

namespace com.aoyon.facetune;

internal static class VirtualAnimationUtility
{
    public static void SetBlendShapes(this VirtualClip clip, string relativePath, IEnumerable<BlendShape> blendShapes)
    {
        if (clip.IsMarkerClip) throw new InvalidOperationException("MarkerClip is not supported");

        foreach (var blendShape in blendShapes)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0, blendShape.Weight);
            clip.SetFloatCurve(relativePath, typeof(SkinnedMeshRenderer), "blendShape." + blendShape.Name, curve);
        }
    }

    public static List<BlendShape> GetBlendShapes(VirtualClip clip, bool first = true)
    {
        var blendShapes = new List<BlendShape>();
        var bindings = clip.GetFloatCurveBindings();
        foreach (var binding in bindings)
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith("blendShape.")) continue;

            var curve = clip.GetFloatCurve(binding);
            if (curve != null && curve.keys.Length > 0)
            {
                var name = binding.propertyName.Replace("blendShape.", string.Empty);
                var weight = first ? curve.keys[0].value : curve.keys[curve.keys.Length - 1].value;
                blendShapes.Add(new BlendShape(name, weight));
            }
        }
        return blendShapes;
    }

    // 適当なGameObjectのactiveを切り替える2フレームアニメーションを作成
    public static VirtualClip CreateCustomEmpty(string clipName = "Custom Empty Clip")
    {
        var clip = VirtualClip.Create(clipName);

        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f / clip.FrameRate, 0f);

        clip.SetFloatCurve("", typeof(GameObject), "m_IsActive", curve);
        return clip;
    }

    public static void SetAnimation(this VirtualClip clip, GenericAnimation animation)
    {
        var binding = animation.CurveBinding.ToEditorCurveBinding();
        clip.SetFloatCurve(binding.path, binding.type, binding.propertyName, animation.Curve);
    }

    public static void SetAnimations(this VirtualClip clip, IEnumerable<GenericAnimation> animations)
    {
        foreach (var animation in animations)
        {
            SetAnimation(clip, animation);
        }
    }
}

