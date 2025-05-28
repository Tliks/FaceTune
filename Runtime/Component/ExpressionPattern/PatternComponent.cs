namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Pattern";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;
        
        internal ExpressionPattern? GetPattern(SessionContext context)
        {
            var expressionWithConditions = gameObject.GetComponentsInChildren<ConditionComponent>()
                .Select(c => c.GetExpressionWithCondition(context, new NonObserveContext()))
                .UnityOfType<ExpressionWithCondition>()
                .ToList();
            if (expressionWithConditions.Count == 0) return null;
            return new ExpressionPattern(expressionWithConditions);
        }
    }
}
