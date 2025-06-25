namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionComponent : ExpressionComponentBase, IHasBlendShapes
    {
        internal const string ComponentName = "FT Facial Expression";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public FacialSettings FacialSettings = new();

        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual
        public bool IsSingleFrame = true;
        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        // FromAnimationClip
        public AnimationClip? Clip;
        public ClipExcludeOption ClipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;


        // Todo: Refactor
        internal override Expression ToExpression(SessionContext sessionContext, DefaultExpressionContext dec, IObserveContext observeContext)
        {
            FacialSettings facialSettings;
            ExpressionSettings expressionSettings = new();

            facialSettings = FacialSettings;
            var sourceMode = observeContext.Observe(this, c => c.SourceMode, (a, b) => a == b);
            switch (sourceMode)
            {
                case AnimationSourceMode.Manual:
                    expressionSettings = ExpressionSettings;
                    break;
                case AnimationSourceMode.FromAnimationClip:
#if UNITY_EDITOR
                    if (Clip == null) break;

                    if (!ExpressionSettings.LoopTime && !string.IsNullOrEmpty(ExpressionSettings.MotionTimeParameterName))
                    {
                        expressionSettings = ExpressionSettings;
                    }
                    else
                    {
                        expressionSettings = ExpressionSettings.FromAnimationClip(Clip);
                    }
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, null);
            }

            var expression = new Expression(name, new List<GenericAnimation>(), expressionSettings, facialSettings);
            
            var defaultExpression = dec.GetDefaultExpression(gameObject);

            var enableBlending = observeContext.Observe(this, c => c.FacialSettings.EnableBlending, (a, b) => a == b);
            if (!enableBlending)
            {
                expression.MergeAnimation(defaultExpression.Animations);
            }

            var diffAnimations = new List<GenericAnimation>();
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

            expression.MergeAnimation(diffAnimations);

            var isSingleFrame = observeContext.Observe(this, c => c.IsSingleFrame, (a, b) => a == b);
            if (isSingleFrame)
            {
                expression.AnimationIndex.AllToSingleFrame();
            }

            return expression;
        }

        internal void GetMergedBlendShapeSet(DefaultExpressionContext dec, ICollection<BlendShape> resultToAdd, IObserveContext? observeContext = null)
        {
            var defaultSet = dec.GetDefaultBlendShapeSet(gameObject);
            GetBlendShapes(resultToAdd, defaultSet, observeContext);
        }

        // defaultsetは結合されていない
        internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet, IObserveContext? observeContext = null)
        {
            observeContext?.Observe(this);
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

        void IHasBlendShapes.GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet, IObserveContext? observeContext)
        {
            GetBlendShapes(resultToAdd, defaultSet, observeContext);
        }
    }
}