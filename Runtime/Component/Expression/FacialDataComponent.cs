namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class ExpressionDataComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Expression Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        // AnimationClip
        public AnimationClip? Clip = null;
        public ClipImportOption ClipOption = ClipImportOption.NonZero;

        // Manual
        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        // Non Facial
        public AnimationClip? NonFacialClip = null;

        internal void GetAnimations(AnimationSet resultToAdd, SessionContext sessionContext)
        {
            resultToAdd.AddRange(ProcessClip().ToGenericAnimations(sessionContext.BodyPath));
            resultToAdd.AddRange(BlendShapeAnimations.ToGenericAnimations(sessionContext.BodyPath)); // Manualを優先
            if (NonFacialClip != null)
            {
                NonFacialClip.GetGenericAnimations(resultToAdd);
            }
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

        internal void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, IObserveContext? observeContext = null)
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