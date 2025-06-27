namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialDataComponent : FaceTuneTagComponent, IAnimationProvider
    {
        internal const string ComponentName = "FT Facial Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual
        public bool IsSingleFrame = true;
        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        // FromAnimationClip
        public AnimationClip? Clip = null;
        public ClipExcludeOption ClipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;

        List<GenericAnimation> IAnimationProvider.GetAnimations(SessionContext sessionContext)
        {
            var animations = new List<GenericAnimation>();
            switch (SourceMode)
            {
                case AnimationSourceMode.Manual:
                    foreach (var animation in BlendShapeAnimations)
                    {
                        if (IsSingleFrame)
                        {
                            animations.Add(animation.ToSingleFrame().ToGeneric(sessionContext.BodyPath));
                        }
                        else
                        {
                            animations.Add(animation.ToGeneric(sessionContext.BodyPath));
                        }
                    }
                    break;
                case AnimationSourceMode.FromAnimationClip:
                    if (Clip == null) break;
                    var blendShapeAnimations = new List<BlendShapeAnimation>();
#if UNITY_EDITOR
                    Clip.GetBlendShapeAnimations(blendShapeAnimations, ClipExcludeOption);
#endif
                    foreach (var animation in blendShapeAnimations)
                    {
                        animations.Add(animation.ToGeneric(sessionContext.BodyPath));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(SourceMode), SourceMode, null);
            }
            return animations;
        }

        internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet, IObserveContext observeContext)
        {
            observeContext.Observe(this);

            switch (SourceMode)
            {
                case AnimationSourceMode.Manual:
                    foreach (var animation in BlendShapeAnimations)
                    {
                        resultToAdd.Add(animation.ToFirstFrameBlendShape());
                    }
                    break;
                case AnimationSourceMode.FromAnimationClip:
                    if (Clip == null) break;
#if UNITY_EDITOR
                    Clip.GetFirstFrameBlendShapes(resultToAdd, ClipExcludeOption, defaultSet);
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(SourceMode), SourceMode, null);
            }
        }
    }
}