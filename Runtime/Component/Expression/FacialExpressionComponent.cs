namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionComponent : ExpressionComponentBase, IHasBlendShapes
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


            var enableBlending = observeContext.Observe(this, c => c.FacialSettings.EnableBlending, (a, b) => a == b);
            if (enableBlending)
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

            return new Expression(name, animations, expressionSettings, facialSettings);
        }

        internal void GetFirstFrameBlendShapeSet(SessionContext sessionContext, ICollection<BlendShape> resultToAdd)
        {
            var defaultSet = sessionContext.DEC.GetDefaultBlendShapeSet(gameObject);
            GetBlendShapes(resultToAdd, defaultSet);
        }

        // defaultsetは結合されていない
        internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet)
        {
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

        void IHasBlendShapes.GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet)
        {
            GetBlendShapes(resultToAdd, defaultSet);
        }
    }
}