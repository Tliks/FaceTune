namespace aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class ExpressionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Expression";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();

        internal Expression ToExpression(SessionContext sessionContext)
        {
            var animationIndex = new AnimationIndex();

            if (!FacialSettings.EnableBlending)
            {
                var zeroAnimations = sessionContext.ZeroWeightBlendShapes
                    .ToGenericAnimations(sessionContext.BodyPath);
                animationIndex.AddRange(zeroAnimations);
            }

            var facialComponent = gameObject.GetComponentInParent<FacialStyleComponent>();
            if (facialComponent != null)
            {
                animationIndex.AddRange(facialComponent.GetAnimations(sessionContext));
            }

            var dataComponents = gameObject.GetInterfacesInChildFTComponents<IAnimationData>(true);
            foreach (var dataComponent in dataComponents)
            {
                animationIndex.AddRange(dataComponent.GetAnimations(sessionContext));
            }

            var facialSettings = FacialSettings;
            var advancedEyeBlinkComponent = gameObject.GetComponentInParent<AdvancedEyBlinkComponent>();
            if (advancedEyeBlinkComponent != null)
            {
                facialSettings = facialSettings with { AdvancedEyBlinkSettings = advancedEyeBlinkComponent.AdvancedEyBlinkSettings };
            }
            
            return new Expression(name, animationIndex.Animations, ExpressionSettings, facialSettings);
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
                var expression = ToExpression(sessionContext);
                yield return new(conditions, expression);
            }
        }
    }
}