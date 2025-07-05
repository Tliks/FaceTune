namespace aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialDataComponent : FaceTuneTagComponent, IAnimationData
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

        List<GenericAnimation> IAnimationData.GetAnimations(SessionContext sessionContext)
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
                case AnimationSourceMode.AnimationClip:
                    if (Clip == null) break;
                    var blendShapeAnimations = new List<BlendShapeAnimation>();
                    ClipToManual(blendShapeAnimations);
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

        internal void ClipToManual(List<BlendShapeAnimation> animations)
        {
            if (Clip == null) return;
            var facialStyleSet = new BlendShapeSet();
            FacialStyleContext.TryAddFacialStyleShapes(gameObject, facialStyleSet);
#if UNITY_EDITOR
            Clip.GetBlendShapeAnimations(animations, ClipExcludeOption, facialStyleSet);
#endif
        }

        internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet facialStyleSet, IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            switch (SourceMode)
            {
                case AnimationSourceMode.Manual:
                    foreach (var animation in BlendShapeAnimations)
                    {
                        resultToAdd.Add(animation.ToFirstFrameBlendShape());
                    }
                    break;
                case AnimationSourceMode.AnimationClip:
                    if (Clip == null) break;
#if UNITY_EDITOR
                    Clip.GetFirstFrameBlendShapes(resultToAdd, ClipExcludeOption, facialStyleSet);
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(SourceMode), SourceMode, null);
            }
        }
    }
}