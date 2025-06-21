namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class MergeExpressionComponent : ExpressionComponentBase
    {
        internal const string ComponentName = "FT Merge Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public FacialSettings FacialSettings = new(); // 影響下にFacialExpressionComponentがある場合のみ有効

        internal Expression? MergedExpression { get; private set; }

        internal override Expression ToExpression(SessionContext sessionContext, IObserveContext observeContext)
        {
            return MergedExpression ?? throw new InvalidOperationException("MergedExpression is not set");
        }

        internal void Merge(SessionContext sessionContext)
        {
            var facialSettings = gameObject.GetComponentsInChildren<FacialExpressionComponent>(true).Any() ? FacialSettings : FacialSettings.Keep;
            var mergedExpression = new Expression(name, new List<GenericAnimation>(), ExpressionSettings, facialSettings);
            var expressionComponents = gameObject.GetComponentsInChildren<ExpressionComponentBase>(true);
            var nonObserveContext = new NonObserveContext();
            foreach (var expressionComponent in expressionComponents)
            {
                if (expressionComponent == this) continue;

                var expression = expressionComponent.ToExpression(sessionContext, nonObserveContext);
                mergedExpression.MergeAnimation(expression.Animations);
                Object.DestroyImmediate(expressionComponent);
            }
            MergedExpression = mergedExpression;
        }
    }
}