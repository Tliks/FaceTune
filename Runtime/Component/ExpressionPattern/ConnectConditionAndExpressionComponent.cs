namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class ConnectConditionAndExpressionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Connect Condition And Expression";
        internal const string MenuPath = FaceTuneTagComponent.FTName + "/" + ComponentName;
        
        public GameObject? ConditionRoot;
        public GameObject? ExpressionRoot;

        internal ExpressionWithCondition GetExpressionWithCondition(SessionContext context)
        {
            return new ExpressionWithCondition(GetConditions(), GetExpressions(context));
        }

        private List<Condition> GetConditions()
        {
            var root = ConditionRoot == null ? gameObject : ConditionRoot;
            return root.GetInterfacesInChildFTComponents<IConditionProvider>()
                .Select(c => c.ToCondition())
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
