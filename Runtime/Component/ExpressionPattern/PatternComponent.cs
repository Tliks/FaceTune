namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Pattern";
        internal const string MenuPath = BasePath + "/" + ExpressionPattern + "/" + ComponentName;
        
        internal ExpressionPattern? GetPattern(SessionContext sessionContext, DefaultExpressionContext dec)
        {
            var expressionWithConditions = gameObject.GetComponentsInChildren<ExpressionComponentBase>(true)
                .SelectMany(c => c.GetExpressionWithConditions(sessionContext, dec))
                .ToList();
            if (expressionWithConditions.Count == 0) return null;
            return new ExpressionPattern(expressionWithConditions);
        }
    }
}
