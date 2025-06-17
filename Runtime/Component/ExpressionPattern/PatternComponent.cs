namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Pattern";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;
        
        internal ExpressionPattern? GetPattern(SessionContext sessionContext)
        {
            var expressionWithConditions = gameObject.GetComponentsInChildren<ConditionComponent>(true)
                .Select(c => c.GetExpressionWithCondition(sessionContext))
                .OfType<ExpressionWithConditions>()
                .ToList();
            if (expressionWithConditions.Count == 0) return null;
            return new ExpressionPattern(expressionWithConditions);
        }
    }
}
