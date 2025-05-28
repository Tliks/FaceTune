namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class ConditionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Condition";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;

        public List<HandGestureCondition> HandGestureConditions = new();
        public List<ParameterCondition> ParameterConditions = new();

        internal bool ExpressionFromSelfOnly = false;

        internal GameObject GetExpressionRoot()
        {
            return gameObject;
        }

        internal ExpressionWithCondition? GetExpressionWithCondition(SessionContext context, IOberveContext observeContext)
        {
            var conditions = GetConditions();
            var expressions = GetExpressions(context, observeContext);
            if (conditions.Count() == 0 || expressions.Count() == 0) return null;
            return new ExpressionWithCondition(conditions.ToList(), expressions.ToList());
        }

        internal IEnumerable<Condition> GetConditions()
        {
            return HandGestureConditions.Select(x => x with { }).Cast<Condition>()
                .Concat(ParameterConditions.Where(x => !string.IsNullOrWhiteSpace(x.ParameterName))
                    .Select(x => x with { }).Cast<Condition>());
        }

        internal IEnumerable<Expression> GetExpressions(SessionContext context, IOberveContext observeContext)
        {
            return GetExpressionComponents(observeContext)
                .Select(c => c as IExpressionProvider)
                .Select(c => c!.ToExpression(context, observeContext))
                .UnityOfType<Expression>()
                .ToList();
        }

        internal IEnumerable<ExpressionComponentBase> GetExpressionComponents(IOberveContext observeContext)
        {
            var fromSelfOnly = observeContext.Observe(this, c => c.ExpressionFromSelfOnly, (a, b) => a == b);
            return fromSelfOnly ? observeContext.GetComponents<ExpressionComponentBase>(gameObject)
                : observeContext.GetComponentsInChildren<ExpressionComponentBase>(gameObject, false);
        }
    }
}