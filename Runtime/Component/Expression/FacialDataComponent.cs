namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class FacialDataComponent : AbstractDataComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Facial Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        // AnimationClip
        public AnimationClip? Clip = null;
        public ClipImportOption ClipOption = ClipImportOption.NonZero;

        // Manual
        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();


        internal override void GetAnimations(AnimationSet resultToAdd, SessionContext sessionContext)
        {
            resultToAdd.AddRange(ProcessClip().ToGenericAnimations(sessionContext.BodyPath));
            resultToAdd.AddRange(BlendShapeAnimations.ToGenericAnimations(sessionContext.BodyPath)); // Manualを優先
        }

        internal List<BlendShapeWeightAnimation> ProcessClip()
        {
            var result = new List<BlendShapeWeightAnimation>();
            if (Clip == null) return result;
            var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
            FacialStyleContext.TryGetFacialStyleAnimations(gameObject, facialStyleAnimations);
#if UNITY_EDITOR
            Clip.GetBlendShapeAnimations(result, ClipOption, facialStyleAnimations);
#endif
            return result;
        }

        internal override void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            if (Clip != null)
            {
#if UNITY_EDITOR
                Clip.GetFirstFrameBlendShapes(resultToAdd, ClipOption, facialAnimations);
#endif
            }

            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation.ToFirstFrameBlendShape());
            }
        }
    }
}