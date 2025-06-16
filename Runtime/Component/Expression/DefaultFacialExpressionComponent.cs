namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class DefaultFacialExpressionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Default Facial Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public FacialSettings FacialSettings = new();
        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        internal Expression GetDefaultExpression(string bodyPath, IObserveContext observeContext)
        {
            var animations = observeContext.Observe(this, c => new List<BlendShapeAnimation>(c.BlendShapeAnimations), (a, b) => a.SequenceEqual(b))
                .Select(ba => ba.ToGeneric(bodyPath))
                .ToList();
            var settings = observeContext.Observe(this, c => c.FacialSettings with {}, (a, b) => a.Equals(b));
            return new Expression(name, animations, settings);
        }

        internal BlendShapeSet GetFirstFrameBlendShapeSet(SessionContext sessionContext, IObserveContext? observeContext = null)
        {
            var expression = GetDefaultExpression(sessionContext.BodyPath, observeContext ?? new NonObserveContext());
            return expression.AnimationIndex.GetAllFirstFrameBlendShapeSet();
        }
    }
}
