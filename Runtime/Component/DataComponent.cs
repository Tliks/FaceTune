namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class DataComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Data";
        internal const string MenuPath = BasePath + "/" + ComponentName;

        // AnimationClip
        public AnimationClip? Clip = null;
        public ClipImportOption ClipOption = ClipImportOption.NonZero;

        // Manual
        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        public bool AllBlendShapeAnimationAsFacial = false;

        internal void GetAnimations(BlendShapeWeightAnimationSet resultToAdd, AvatarContext avatarContext)
        {
            GetBlendShapeAnimations(resultToAdd, Array.Empty<BlendShapeWeightAnimation>(), avatarContext.BodyPath);
        }

        internal void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, string bodyPath, IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            if (Clip != null)
            {
                var facialPath = AllBlendShapeAnimationAsFacial ? null : bodyPath;
#if UNITY_EDITOR
                Clip.GetFirstFrameBlendShapes(ClipOption, resultToAdd, facialPath, facialAnimations);
#endif
            }

            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation.ToFirstFrameBlendShape());
            }
        }

        internal void GetBlendShapeAnimations(ICollection<BlendShapeWeightAnimation> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, string bodyPath, IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            if (Clip != null)
            {
                var facialPath = AllBlendShapeAnimationAsFacial ? null : bodyPath;
#if UNITY_EDITOR
                Clip.GetBlendShapeAnimations(ClipOption, resultToAdd, facialPath, facialAnimations);
#endif
            }

            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation);
            }
        }
    }
}