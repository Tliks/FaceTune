namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class ExpressionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Expression";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();

        public bool EnableRealTimePreview = false;

        internal AvatarExpression ToExpression(SessionContext sessionContext)
        {
            var animationIndex = new AnimationSet();

            if (!FacialSettings.EnableBlending)
            {
                var zeroAnimations = sessionContext.SafeZeroBlendShapes.ToGenericAnimations(sessionContext.BodyPath);
                animationIndex.AddRange(zeroAnimations);

                using var _ = ListPool<BlendShapeWeightAnimation>.Get(out var facialAnimations);
                if (FacialStyleContext.TryGetFacialStyleAnimations(gameObject, facialAnimations))
                {
                    animationIndex.AddRange(facialAnimations.ToGenericAnimations(sessionContext.BodyPath));
                }
            }

            var dataComponents = gameObject.GetInterfacesInChildFaceTuneComponents<AbstractDataComponent>(true);
            foreach (var dataComponent in dataComponents)
            {
                animationIndex.AddRange(dataComponent.GetAnimations(sessionContext));
            }

            var advancedEyeBlinkComponent = gameObject.GetComponentInParent<AdvancedEyeBlinkComponent>(true);
            var blinkSettings = advancedEyeBlinkComponent == null
                ? AdvancedEyeBlinkSettings.Disabled() 
                : advancedEyeBlinkComponent.AdvancedEyeBlinkSettings;

            var advancedLipSyncComponent = gameObject.GetComponentInParent<AdvancedLipSyncComponent>(true);
            var lipSyncSettings = advancedLipSyncComponent == null
                ? AdvancedLipSyncSettings.Disabled()
                : advancedLipSyncComponent.AdvancedLipSyncSettings;

            var facialSettings = FacialSettings with {
                AdvancedEyBlinkSettings = blinkSettings,
                AdvancedLipSyncSettings = lipSyncSettings
            };

            return new AvatarExpression(name, animationIndex.Animations, ExpressionSettings, facialSettings);
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