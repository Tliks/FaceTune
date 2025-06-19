namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class AnimationExpressionComponent : ExpressionComponentBase
    {
        internal const string ComponentName = "FT Animation Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public AnimationSourceMode SourceMode = AnimationSourceMode.FromAnimationClip;

        // Manual
        public List<GenericAnimation> GenericAnimations = new();

        // FromAnimationClip
        public AnimationClip? Clip = null;

        internal override Expression ToExpression(SessionContext sessionContext, IObserveContext observeContext)
        {
            var animations = new List<GenericAnimation>();
            ExpressionSettings expressionSettings = new();

            var sourceMode = observeContext.Observe(this, c => c.SourceMode, (a, b) => a == b);
            switch (sourceMode)
            {
                case AnimationSourceMode.Manual:
                    var genericAnimations = observeContext.Observe(this, c => c.GenericAnimations, (a, b) => a == b);
                    animations.AddRange(genericAnimations);
                    expressionSettings = ExpressionSettings;
                    break;
                case AnimationSourceMode.FromAnimationClip:
                    var clip = observeContext.Observe(this, c => c.Clip, (a, b) => a == b);
                    if (clip != null)
                    {
#if UNITY_EDITOR
                        animations.AddRange(GenericAnimation.FromAnimationClip(clip));
                        expressionSettings = ExpressionSettings.FromAnimationClip(clip);
#endif
                    }
                    else
                    {
                        expressionSettings = new ExpressionSettings();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, null);
            }

            return new Expression(name, animations, expressionSettings);
        }
    }
}