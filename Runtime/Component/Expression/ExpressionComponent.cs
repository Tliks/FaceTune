namespace com.aoyon.facetune
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ExpressionDataComponent))]
    [AddComponentMenu(MenuPath)]
    public class ExpressionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Expression";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();

        internal Expression ToExpression(SessionContext sessionContext, IObserveContext observeContext)
        {
            var animations = new List<GenericAnimation>();
            var dataComponents = gameObject.GetComponentsInChildren<ExpressionDataComponent>(true);
            foreach (var dataComponent in dataComponents)
            {
                animations.AddRange(dataComponent.GetAnimations(sessionContext, observeContext));
            }
            return new Expression(name, animations, ExpressionSettings, FacialSettings);
        }

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
    }
}