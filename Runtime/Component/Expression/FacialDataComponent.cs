namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class FacialDataComponent : AbstractDataComponent
    {
        internal const string ComponentName = "FT Facial Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual
        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

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
                    var blendShapeAnimations = new List<BlendShapeWeightAnimation>();
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

        internal void ClipToManual(List<BlendShapeWeightAnimation> animations)
        {
            if (Clip == null) return;
            var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
            FacialStyleContext.TryGetFacialStyleAnimations(gameObject, facialStyleAnimations);
#if UNITY_EDITOR
            Clip.GetBlendShapeAnimations(animations, ClipOption, facialStyleAnimations);
#endif
        }

        internal override void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, IObserveContext? observeContext = null)
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