namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class ConditionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Condition";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;

        public List<HandGestureCondition> HandGestureConditions = new();
        public List<ParameterCondition> ParameterConditions = new();

        public GameObject? OverrideExpressionRoot = null;

        internal GameObject GetExpressionRoot()
        {
            return OverrideExpressionRoot == null ? gameObject : OverrideExpressionRoot;
        }

        internal ExpressionWithCondition? GetExpressionWithCondition(SessionContext context)
        {
            var conditions = GetConditions();
            var expressions = GetExpressions(context);
            if (conditions.Count() == 0 || expressions.Count() == 0) return null;
            return new ExpressionWithCondition(conditions.ToList(), expressions.ToList());
        }

        IEnumerable<Condition> GetConditions()
        {
            return HandGestureConditions.Select(x => x with { }).Cast<Condition>()
                .Concat(ParameterConditions.Where(x => !string.IsNullOrWhiteSpace(x.ParameterName))
                    .Select(x => x with { }).Cast<Condition>());
        }

        IEnumerable<Expression> GetExpressions(SessionContext context)
        {
            var root = GetExpressionRoot();
            return root.GetInterfacesInChildFTComponents<IExpressionProvider>()
                .Select(c => c.ToExpression(context))
                .UnityOfType<Expression>()
                .ToList();
        }
    }
}
