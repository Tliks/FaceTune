namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class AnimationExpressionComponent : ExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT AnimationExpression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public AnimationSourceMode SourceMode = AnimationSourceMode.FromAnimationClip;

        // Manual
        public List<GenericAnimation> GenericAnimations = new();

        // FromAnimationClip
        public AnimationClip? Clip = null;

        Expression IExpressionProvider.ToExpression(SessionContext sessionContext, IObserveContext observeContext)
        {
            var animations = new List<GenericAnimation>();

            var sourceMode = observeContext.Observe(this, c => c.SourceMode, (a, b) => a == b);
            switch (sourceMode)
            {
                case AnimationSourceMode.Manual:
                    var genericAnimations = observeContext.Observe(this, c => c.GenericAnimations, (a, b) => a == b);
                    animations.AddRange(genericAnimations.Select(ga => ga with {}));
                    break;
                case AnimationSourceMode.FromAnimationClip:
#if UNITY_EDITOR
                    var clip = observeContext.Observe(this, c => c.Clip, (a, b) => a == b);
                    if (clip == null) break;
                    animations.AddRange(GenericAnimation.FromAnimationClip(clip).Select(ga => ga with {}));
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceMode), sourceMode, null);
            }

            return new Expression(name, animations);
        }
    }
}