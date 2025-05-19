namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class ConnectConditionAndExpressionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Connect Condition And Expression";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;
        
        public GameObject? ConditionRoot;
        public GameObject? ExpressionRoot;

        internal ExpressionWithCondition? GetExpressionWithCondition(SessionContext context)
        {
            var conditions = GetConditions();
            var expressions = GetExpressions(context);
            if (conditions.Count == 0 || expressions.Count == 0) return null;
            return new ExpressionWithCondition(conditions, expressions);
        }

        private List<Condition> GetConditions()
        {
            var root = ConditionRoot == null ? gameObject : ConditionRoot;
            return root.GetInterfacesInChildFTComponents<IConditionProvider>()
                .Select(c => c.ToCondition())
                .OfType<Condition>()
                .ToList();
        }

        private List<Expression> GetExpressions(SessionContext context)
        {
            var root = ExpressionRoot == null ? gameObject : ExpressionRoot;
            return root.GetInterfacesInChildFTComponents<IExpressionProvider>()
                .Select(c => c.ToExpression(context))
                .OfType<Expression>()
                .ToList();
        }
    }
}
