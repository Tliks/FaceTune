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

        public bool AllBlendShapeAnimationAsFacial = false;

        internal void GetAnimations(AnimationSet resultToAdd, AvatarContext avatarContext)
        {
            var (facialAnimations, nonFacialAnimations) = ProcessClip(avatarContext.BodyPath);
            resultToAdd.AddRange(facialAnimations.ToGenericAnimations(avatarContext.BodyPath));
            resultToAdd.AddRange(nonFacialAnimations);
            resultToAdd.AddRange(BlendShapeAnimations.ToGenericAnimations(avatarContext.BodyPath)); // Manualを優先
        }

        internal (List<BlendShapeWeightAnimation> facialAnimations, List<GenericAnimation> nonFacialAnimations) ProcessClip(string bodyPath)
        {
            var result = (facialAnimations: new List<BlendShapeWeightAnimation>(), nonFacialAnimations: new List<GenericAnimation>());
            if (Clip == null) return result;
            var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
            FacialStyleContext.TryGetFacialStyleAnimations(gameObject, facialStyleAnimations);
            var facialPath = AllBlendShapeAnimationAsFacial ? null : bodyPath;
#if UNITY_EDITOR
            Clip.ProcessAllBindings(ClipOption, facialStyleAnimations, result.facialAnimations, result.nonFacialAnimations, facialPath);
#endif
            return result;
        }

        internal void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, string bodyPath, IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            if (Clip != null)
            {
                var facialPath = AllBlendShapeAnimationAsFacial ? null : bodyPath;
#if UNITY_EDITOR
                Clip.GetFirstFrameBlendShapes(resultToAdd, ClipOption, facialAnimations, facialPath);
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
                Clip.GetBlendShapeAnimations(resultToAdd, ClipOption, facialAnimations, facialPath);
#endif
            }

            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation);
            }
        }
    }
}