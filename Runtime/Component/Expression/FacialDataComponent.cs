namespace aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialDataComponent : AbstractDataComponent
    {
        internal const string ComponentName = "FT Facial Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual
        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        // FromAnimationClip
        public AnimationClip? Clip = null;
        public ClipImportOption ClipOption = ClipImportOption.NonZero;

        internal override List<GenericAnimation> GetAnimations(SessionContext sessionContext)
        {
            var animations = new List<GenericAnimation>();
            switch (SourceMode)
            {
                case AnimationSourceMode.Manual:
                    foreach (var animation in BlendShapeAnimations)
                    {
                        animations.Add(animation.ToGeneric(sessionContext.BodyPath));
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
            var facialStyleAnimations = new List<BlendShapeAnimation>();
            FacialStyleContext.TryGetFacialStyleAnimations(gameObject, facialStyleAnimations);
#if UNITY_EDITOR
            Clip.GetBlendShapeAnimations(animations, ClipOption, facialStyleAnimations);
#endif
        }

        internal override void GetBlendShapes(ICollection<BlendShape> resultToAdd, IReadOnlyList<BlendShapeAnimation> facialAnimations, IObserveContext? observeContext = null)
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
                    Clip.GetFirstFrameBlendShapes(resultToAdd, ClipOption, facialAnimations);
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(SourceMode), SourceMode, null);
            }
        }
    }
}