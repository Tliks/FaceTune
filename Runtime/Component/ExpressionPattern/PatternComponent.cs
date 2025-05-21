namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Pattern";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;
        
        public int Priority = 0;

        internal (ExpressionPattern, int)? GetPatternWithPriority(SessionContext context)
        {
            var expressionWithConditions = gameObject.GetComponentsInChildren<ConditionComponent>()
                .Select(c => c.GetExpressionWithCondition(context))
                .OfType<ExpressionWithCondition>()
                .ToList();
            if (expressionWithConditions.Count == 0) return null;
            return (new ExpressionPattern(expressionWithConditions), Priority);
        }
    }
}
