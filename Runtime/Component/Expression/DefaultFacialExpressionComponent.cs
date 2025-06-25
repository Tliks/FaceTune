namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class DefaultFacialExpressionComponent : FaceTuneTagComponent, IHasBlendShapes
    {
        internal const string ComponentName = "FT Default Facial Expression";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public FacialSettings FacialSettings = new();
        public List<BlendShapeAnimation> BlendShapeAnimations = new();
        public ExpressionSettings ExpressionSettings = new();

        internal Expression GetDefaultExpression(List<string> defaultBlendShapeNames, string bodyPath, IObserveContext observeContext)
        {
            observeContext.Observe(this);
            var animations = new List<GenericAnimation>();
            animations.AddRange(defaultBlendShapeNames.Select(bs => BlendShapeAnimation.SingleFrame(bs, 0).ToGeneric(bodyPath)));
            animations.AddRange(BlendShapeAnimations.Select(ba => ba.ToGeneric(bodyPath)));
            return new Expression(name, animations, ExpressionSettings, FacialSettings);
        }

        internal BlendShapeSet GetMergedBlendShapeSet(SessionContext sessionContext, IObserveContext? observeContext = null)
        {
            var defaultBlendShapeNames = sessionContext.FaceRenderer.GetBlendShapes(sessionContext.FaceMesh).Select(bs => bs.Name).ToList();
            var expression = GetDefaultExpression(defaultBlendShapeNames, sessionContext.BodyPath, observeContext ?? new NonObserveContext());
            return expression.AnimationIndex.GetAllFirstFrameBlendShapeSet();
        }

        internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet, IObserveContext? observeContext)
        {
            observeContext?.Observe(this);
            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation.ToFirstFrameBlendShape());
            }
        }

        void IHasBlendShapes.GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet, IObserveContext? observeContext)
        {
            GetBlendShapes(resultToAdd, defaultSet, observeContext);
        }
    }
}