namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression ToExpression(FacialExpression defaultExpression, IObserveContext observeContext);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent, IExpressionProvider
{
    internal bool ExpressionFromSelfOnly = false;

    internal IEnumerable<ExpressionWithCondition> GetExpressionWithConditions(FacialExpression defaultExpression)
    {
        // 親の GameObject ごとの Condition を取得する (OR の AND)
        var conditionComponentsByGameObject = new List<ConditionComponent[]>();
        var current = transform;
        while (current != null && (current == transform || !ExpressionFromSelfOnly))
        {
            var conditionComponents = current.GetComponents<ConditionComponent>();
            if (conditionComponents.Length > 0)
            {
                conditionComponentsByGameObject.Add(conditionComponents);
            }
            current = current.parent;
        }

        // 親の GameObject ごとの Condition の直積を求める (AND の OR)
        var conditionComponentsByExpression = conditionComponentsByGameObject
            .Aggregate(
                Enumerable.Repeat(Enumerable.Empty<ConditionComponent>(), 1),
                (acc, set) => acc.SelectMany(_ => set, (x, y) => x.Append(y))
            );

        foreach (var conditionComponents in conditionComponentsByExpression)
        {
            var conditions = conditionComponents
                .SelectMany(x => Enumerable.Concat<Condition>(
                    x.HandGestureConditions.Select(y => y with { }),
                    x.ParameterConditions.Select(y => y with { })))
                .ToList();
            var expressions = new[] { ToExpression(defaultExpression, new NonObserveContext()) };
            yield return new(conditions, expressions);
        }
    }

    internal abstract Expression ToExpression(FacialExpression defaultExpression, IObserveContext observeContext);

    Expression IExpressionProvider.ToExpression(FacialExpression defaultExpression, IObserveContext observeContext)
    {
        return ToExpression(defaultExpression, observeContext);
    }
}