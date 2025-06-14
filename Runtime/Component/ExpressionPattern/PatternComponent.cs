namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Pattern";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;
        
        internal ExpressionPattern? GetPattern(FacialExpression defaultExpression)
        {
            var expressionWithConditions = new List<ExpressionWithCondition>();
            foreach (var (expression, conditions) in gameObject.GetComponentsInChildren<ExpressionComponentBase>(true)
                .SelectMany(x => x.GetConditions(), (x, y) => (x.ToExpression(defaultExpression, new NonObserveContext()), y)))
            {
                var expressionWithCondition = expressionWithConditions.SingleOrDefault(x => x.Conditions.SequenceEqual(conditions));
                if (expressionWithCondition == null)
                {
                    expressionWithConditions.Add(new(conditions.ToList(), new[] { expression }));
                }
                else
                {
                    var expressions = expressionWithCondition.Expressions.ToList();
                    expressions.Add(expression);
                    expressionWithCondition.SetExpressions(expressions);
                }
            }
            if (expressionWithConditions.Count == 0) return null;
            return new ExpressionPattern(expressionWithConditions);
        }
    }
}
