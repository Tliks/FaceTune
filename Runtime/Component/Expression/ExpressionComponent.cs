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

        internal AvatarExpression ToExpression(AvatarContext avatarContext)
        {
            var animationSet = new AnimationSet();

            if (!FacialSettings.EnableBlending)
            {
                var zeroAnimations = avatarContext.SafeZeroBlendShapes.ToGenericAnimations(avatarContext.BodyPath);
                animationSet.AddRange(zeroAnimations);

                using var _ = ListPool<BlendShapeWeightAnimation>.Get(out var facialAnimations);
                if (FacialStyleContext.TryGetFacialStyleAnimations(gameObject, facialAnimations))
                {
                    animationSet.AddRange(facialAnimations.ToGenericAnimations(avatarContext.BodyPath));
                }
            }

            var dataComponents = gameObject.GetComponentsInChildren<ExpressionDataComponent>(true);
            foreach (var dataComponent in dataComponents)
            {
                dataComponent.GetAnimations(animationSet, avatarContext);
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

            return new AvatarExpression(name, animationSet.Animations, ExpressionSettings, facialSettings);
        }

        internal ExpressionWithCondition GetExpressionWithConditions(AvatarContext avatarContext)
        {
            var conditionTree = BuildConditionTreeFromHierarchy(avatarContext);
            var expression = ToExpression(avatarContext);
            return new(conditionTree, expression); 
        }

        private ICondition BuildConditionTreeFromHierarchy(AvatarContext avatarContext)
        {
            List<ICondition> conditions = new();
            var current = transform;

            // 1. 親をたどりながら、各GameObjectが持つ条件を収集
            while (current != null)
            {
                var conditionComponents = current.GetComponents<ConditionComponent>();
                if (conditionComponents.Length > 0)
                {
                    // 2. 同じGameObject上の条件はすべてORで結合する
                    List<ICondition> orConditionsOnGameObject = new();
                    foreach (var conditionComponent in conditionComponents)
                    {
                        orConditionsOnGameObject.Add(conditionComponent.ToCondition(avatarContext));
                    }
                    if (orConditionsOnGameObject.Count > 0)
                    {
                        conditions.Add(new OrCondition(orConditionsOnGameObject));
                    }
                }
                current = current.parent;
            }
            
            if (conditions.Count == 0)
            {
                return TrueCondition.Instance; // Todo: これで良いか考える
            }

            // 3. GameObject間の条件はすべてAndで結合する
            return new AndCondition(conditions);
        }    
    }
}