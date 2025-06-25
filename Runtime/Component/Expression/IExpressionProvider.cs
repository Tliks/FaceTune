namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression ToExpression(SessionContext sessionContext, IObserveContext observeContext); // ビルドでのみ呼ぶ
}

internal interface IHasBlendShapes
{
    internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet, IObserveContext? observeContext = null);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent, IExpressionProvider
{
    public ExpressionSettings ExpressionSettings = new();
    
    internal IEnumerable<ExpressionWithConditions> GetExpressionWithConditions(SessionContext sessionContext)
    {
        // 親の GameObject ごとの Condition を取得する (OR の AND)
        var conditionComponentsByGameObject = new List<ConditionComponent[]>();
        var current = transform;
        while (current != null)
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
            var expression = ToExpression(sessionContext, new NonObserveContext());
            yield return new(conditions, expression);
        }
    }

    internal abstract Expression ToExpression(SessionContext sessionContext, IObserveContext observeContext);

    Expression IExpressionProvider.ToExpression(SessionContext sessionContext, IObserveContext observeContext)
    {
        return ToExpression(sessionContext, observeContext);
    }
}