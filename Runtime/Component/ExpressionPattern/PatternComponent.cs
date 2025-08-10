namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Pattern";
        internal const string MenuPath = BasePath + "/" + ExpressionPattern + "/" + ComponentName;
        
        internal ExpressionPattern? GetPattern(SessionContext sessionContext)
        {
            var expressionWithConditions = gameObject.GetComponentsInChildren<ExpressionComponent>(true)
                .SelectMany(c => c.GetExpressionWithConditions(sessionContext))
                .ToList();
            if (expressionWithConditions.Count == 0) return null;
            return new ExpressionPattern(expressionWithConditions);
        }
    }
}
