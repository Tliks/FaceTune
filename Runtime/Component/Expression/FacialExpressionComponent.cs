namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionComponent : ExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT Facial Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public FacialSettings FacialSettings = new();

        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual
        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        // FromAnimationClip
        public AnimationClip? Clip;
        public ClipExcludeOption ClipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;

        Expression IExpressionProvider.ToExpression(SessionContext sessionContext, IObserveContext observeContext)
        {
            var defaultExpression = sessionContext.DEC.GetDefaultExpression(gameObject);

            var diffAnimations = new List<GenericAnimation>();
            var sourceMode = observeContext.Observe(this, c => c.SourceMode, (a, b) => a == b);
            switch (sourceMode)
            {
                case AnimationSourceMode.Manual:
                    var blendShapeAnimations = observeContext.Observe(this, c => c.BlendShapeAnimations, (a, b) => a == b);
                    diffAnimations.AddRange(blendShapeAnimations.Select(ba => ba.GetGeneric(sessionContext.BodyPath)));
                    break;
                case AnimationSourceMode.FromAnimationClip:
#if UNITY_EDITOR
                    var clip = observeContext.Observe(this, c => c.Clip, (a, b) => a == b);
                    if (clip == null) break;

                    var blendShapeAnimations_ = AnimationUtility.FilterBlendShapeAnimations(GenericAnimation.FromAnimationClip(clip)).ToList();
                    var animationIndex = new AnimationIndex(blendShapeAnimations_);
                    
                    var excludeOption = observeContext.Observe(this, c => c.ClipExcludeOption, (a, b) => a == b);
                    var defaultSet = defaultExpression.AnimationIndex.GetAllFirstFrameBlendShapeSet();

                    var namesToRemove = new List<string>();
                    switch (excludeOption)
                    {
                        case ClipExcludeOption.None:
                            break;
                        case ClipExcludeOption.ExcludeZeroWeight:
                            var set = animationIndex.GetAllFirstFrameBlendShapeSet();
                            var zeroWeightBlendShapeNames = set.GetMapping().Where(x => x.Value.Weight == 0).Select(x => x.Key).ToList();
                            namesToRemove.AddRange(zeroWeightBlendShapeNames);
                            break;
                        case ClipExcludeOption.ExcludeDefault:
                            var defaultSetMapping = defaultSet.GetMapping();
                            foreach (var name in animationIndex.GetAllBlendShapeNames())
                            {
                                if (defaultSetMapping.TryGetValue(name, out var blendShape) && blendShape.Weight == 0)
                                {
                                    namesToRemove.Add(name);
                                }
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(excludeOption), excludeOption, null);
                    }
                    animationIndex.RemoveBlendShapes(namesToRemove);

                    diffAnimations.AddRange(animationIndex.Animations.Select(ga => ga with {}));
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, null);
            }

            List<GenericAnimation> ret;

            var blendingPermission = observeContext.Observe(this, c => c.FacialSettings.BlendingPermission, (a, b) => a == b);
            if (blendingPermission == BlendingPermission.Disallow)
            {
                ret = diffAnimations.Select(ga => ga with {}).ToList();
            }
            else
            {
                var animationIndex = defaultExpression.AnimationIndex;
                animationIndex.MergeAnimation(diffAnimations);
                ret = animationIndex.Animations.Select(ga => ga with {}).ToList();
            }

            return new Expression(name, ret, FacialSettings with {});
        }

        internal BlendShapeSet GetFirstFrameBlendShapeSet(SessionContext sessionContext, IObserveContext? observeContext = null)
        {
            var expression = (this as IExpressionProvider)!.ToExpression(sessionContext, observeContext ?? new NonObserveContext());
            return expression.AnimationIndex.GetAllFirstFrameBlendShapeSet();
        }
    }
}