namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionComponent : ExpressionComponentBase
    {
        internal const string ComponentName = "FT Facial Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public FacialSettings FacialSettings = new();

        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual
        public bool IsSingleFrame = true;
        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        // FromAnimationClip
        public AnimationClip? Clip;
        public ClipExcludeOption ClipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;


        // Todo: Refactor
        internal override Expression ToExpression(SessionContext sessionContext, IObserveContext observeContext)
        {
            List<GenericAnimation> animations;
            
            var defaultExpression = sessionContext.DEC.GetDefaultExpression(gameObject);

            var diffAnimations = new List<GenericAnimation>();
            var sourceMode = observeContext.Observe(this, c => c.SourceMode, (a, b) => a == b);
            switch (sourceMode)
            {
                case AnimationSourceMode.Manual:
                    var blendShapeAnimations = observeContext.Observe(this, c => new List<BlendShapeAnimation>(c.BlendShapeAnimations), (a, b) => a.SequenceEqual(b));
                    diffAnimations.AddRange(blendShapeAnimations.Select(ba => ba.ToGeneric(sessionContext.BodyPath)));
                    break;
                case AnimationSourceMode.FromAnimationClip:
#if UNITY_EDITOR
                    var clip = observeContext.Observe(this, c => c.Clip, (a, b) => a == b);
                    if (clip == null) break;

                    var blendShapeAnimations_ = GenericAnimation.FromAnimationClip(clip).Where(a => a.IsBlendShapeAnimation()).ToList();
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
                            var zeroWeightBlendShapeNames = set.Where(x => x.Weight == 0).Select(x => x.Name).ToList();
                            namesToRemove.AddRange(zeroWeightBlendShapeNames);
                            break;
                        case ClipExcludeOption.ExcludeDefault:
                            foreach (var name in animationIndex.GetAllBlendShapeNames())
                            {
                                if (defaultSet.TryGetValue(name, out var blendShape) && blendShape.Weight == 0)
                                {
                                    namesToRemove.Add(name);
                                }
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(excludeOption), excludeOption, null);
                    }
                    animationIndex.RemoveBlendShapes(namesToRemove);

                    diffAnimations.AddRange(animationIndex.Animations);
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, null);
            }


            var blendingPermission = observeContext.Observe(this, c => c.FacialSettings.BlendingPermission, (a, b) => a == b);
            if (blendingPermission == BlendingPermission.Disallow)
            {
                animations = diffAnimations;
            }
            else
            {
                var animationIndex = defaultExpression.AnimationIndex;
                animationIndex.MergeAnimation(diffAnimations);
                animations = animationIndex.Animations.ToList();
            }

            var isSingleFrame = observeContext.Observe(this, c => c.IsSingleFrame, (a, b) => a == b);
            if (isSingleFrame)
            {
                for (int i = 0; i < animations.Count; i++)
                {
                    animations[i] = animations[i].ToSingleFrame();
                }
            }

            FacialSettings facialSettings;
            ExpressionSettings expressionSettings = new();

            facialSettings = FacialSettings;
            switch (sourceMode)
            {
                case AnimationSourceMode.Manual:
                    expressionSettings = ExpressionSettings;
                    break;
                case AnimationSourceMode.FromAnimationClip:
#if UNITY_EDITOR
                    if (Clip != null) expressionSettings = ExpressionSettings.FromAnimationClip(Clip);
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, null);
            }

            return new Expression(name, animations, expressionSettings, facialSettings);
        }

        internal BlendShapeSet GetFirstFrameBlendShapeSet(SessionContext sessionContext, IObserveContext? observeContext = null)
        {
            var expression = (this as IExpressionProvider)!.ToExpression(sessionContext, observeContext ?? new NonObserveContext());
            return expression.AnimationIndex.GetAllFirstFrameBlendShapeSet();
        }
    }
}