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

        internal Expression GetDefaultExpression(string bodyPath, IObserveContext observeContext)
        {
            observeContext.Observe(this);
            var animations = new List<GenericAnimation>(BlendShapeAnimations.Select(ba => ba.ToGeneric(bodyPath)));
            return new Expression(name, animations, ExpressionSettings, FacialSettings);
        }

        internal BlendShapeSet GetFirstFrameBlendShapeSet(SessionContext sessionContext, IObserveContext? observeContext = null)
        {
            var expression = GetDefaultExpression(sessionContext.BodyPath, observeContext ?? new NonObserveContext());
            return expression.AnimationIndex.GetAllFirstFrameBlendShapeSet();
        }

        void IHasBlendShapes.GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet, IObserveContext? observeContext)
        {
            observeContext?.Observe(this);
            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation.ToFirstFrameBlendShape());
            }
        }
    }
}