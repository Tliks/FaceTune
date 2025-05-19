namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Pattern";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;
        
        public int Priority = 0;

        internal (ExpressionPattern, int) GetPatternWithPriority(SessionContext context)
        {
            var expression = gameObject.GetComponentsInChildren<ConnectConditionAndExpressionComponent>()
                .Select(c => c.GetExpressionWithCondition(context))
                .ToList();
            return (new ExpressionPattern(expression), Priority);
        }
    }
}
