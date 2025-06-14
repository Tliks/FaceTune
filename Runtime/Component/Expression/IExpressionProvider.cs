namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression ToExpression(FacialExpression defaultExpression, IObserveContext observeContext);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent, IExpressionProvider
{
    internal bool ExpressionFromSelfOnly = false;

    internal IEnumerable<IEnumerable<Condition>> GetConditions()
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
        return conditionComponentsByGameObject
            .Aggregate(Enumerable.Repeat(Enumerable.Empty<Condition>(), 1), (conditions, components) => conditions
                .SelectMany(_ => components, (x, y) => x
                    .Concat(y.HandGestureConditions.Select(z => z with { }))
                    .Concat(y.ParameterConditions.Select(z => z with { }))));
    }

    internal abstract Expression ToExpression(FacialExpression defaultExpression, IObserveContext observeContext);

    Expression IExpressionProvider.ToExpression(FacialExpression defaultExpression, IObserveContext observeContext)
    {
        return ToExpression(defaultExpression, observeContext);
    }
}