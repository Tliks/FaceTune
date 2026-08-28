namespace Aoyon.FaceTune;

[Serializable]
internal sealed class NonFacialAnimationData
{
    public List<AnimationClip> AnimationClips = new();
    public List<TransformAnimation> TransformAnimations = new();
}

[Serializable]
internal sealed class TransformAnimation
{
    public AvatarObjectReference Target = new();
    public AnimationCurve Curve = AnimationCurve.Constant(0f, 1f, 1f);
}
