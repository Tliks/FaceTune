using com.aoyon.facetune.platform;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

// Hierarchy上のデータを弄るパスは基本的にここに置く
// なお、PatternDataを必要とする場合は後続のパス。
internal class ModifyHierarchyPass : Pass<ModifyHierarchyPass>
{
    public override string QualifiedName => "com.aoyon.facetune.modify-hierarchy";
    public override string DisplayName => "Modify Hierarchy";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.Extension<FTPassContext>()!;
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;
        
        // Condition
        NegotiateMAMenuItem(passContext);
        // Expression
        MergeExpression(sessionContext);
        // Pattern
        NormalizeData(context.AvatarRootObject);
    }

    private void NegotiateMAMenuItem(FTPassContext passContext)
    {
        var root = passContext.BuildContext.AvatarRootObject;
        var platformSupport = passContext.PlatformSupport;

        var usedParameterNames = new HashSet<string>();
        var menuItems = root.GetComponentsInChildren<ModularAvatarMenuItem>(true);
        foreach (var menuItem in menuItems)
        {
            var parameterName = platformSupport.GetParameterName(menuItem);
            if (string.IsNullOrWhiteSpace(parameterName)) continue;
            usedParameterNames.Add(parameterName);
        }

        foreach (var menuItem in menuItems)
        {    
            using var _ = ListPool<ExpressionComponentBase>.Get(out var expressionComponents);
            expressionComponents.AddRange(menuItem.GetComponentsInChildren<ExpressionComponentBase>(true));
            if (expressionComponents.Any() is false) continue;

            var menuItemType = platformSupport.GetMenuItemType(menuItem);

            switch (menuItemType)
            {
                case MenuItemType.Toggle:
                case MenuItemType.Button:
                    var parameterName = EnsureParameter(menuItem, usedParameterNames, platformSupport);
                    platformSupport.SetParameterValue(menuItem, 1);
                    var conditionComponent = menuItem.gameObject.AddComponent<ConditionComponent>(); // OR
                    conditionComponent.ParameterConditions.Add(ParameterCondition.Bool(parameterName, true));
                    break;
                case MenuItemType.RadialPuppet:
                    var radialParameterName = EnsureRadialParameter(menuItem, usedParameterNames, platformSupport);
                    foreach (var expressionComponent in expressionComponents)
                    {
                        var settings = expressionComponent.ExpressionSettings;
                        expressionComponent.ExpressionSettings = settings with { LoopTime = false, MotionTimeParameterName = radialParameterName };
                    }
                    break;
                default:
                    break;
            }
        }

        static string EnsureParameter(ModularAvatarMenuItem menuItem, HashSet<string> usedParameterNames, IPlatformSupport platformSupport)
        {
            string parameterName = platformSupport.GetParameterName(menuItem);
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                parameterName = platformSupport.GetUniqueParameterName(menuItem, usedParameterNames, "toggle");
                usedParameterNames.Add(parameterName);
                platformSupport.SetParameterName(menuItem, parameterName);
            }
            return parameterName;
        }

        static string EnsureRadialParameter(ModularAvatarMenuItem menuItem, HashSet<string> usedParameterNames, IPlatformSupport platformSupport)
        {
            string parameterName = platformSupport.GetRadialParameterName(menuItem);
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                parameterName = platformSupport.GetUniqueParameterName(menuItem, usedParameterNames, "radial");
                usedParameterNames.Add(parameterName);
                platformSupport.SetRadialParameterName(menuItem, parameterName);
            }
            var parameters = menuItem.gameObject.EnsureComponent<ModularAvatarParameters>();
            parameters.parameters.Add(new ParameterConfig()
            {
                nameOrPrefix = parameterName,
                syncType = ParameterSyncType.Float,
                defaultValue = 0,
            });
            return parameterName;
        }
    }

    private void MergeExpression(SessionContext sessionContext)
    {
        var mergeExpressionComponents = sessionContext.Root.GetComponentsInChildren<MergeExpressionComponent>(true);
        foreach (var mergeExpressionComponent in mergeExpressionComponents)
        {
            mergeExpressionComponent.Merge(sessionContext);
        }
    }

    private void NormalizeData(GameObject root)
    {
        // Patternに属しないExpressionをそれぞれ単一のPatternとして扱うことでデータを正規化する
        var expressionComponents = root.GetComponentsInChildren<ExpressionComponentBase>(true);
        foreach (var expressionComponent in expressionComponents)
        {
            if (expressionComponent.GetComponentInParentNullable<PatternComponent>() == null)
            {
                expressionComponent.gameObject.EnsureComponent<PatternComponent>();
            }
        }
    }
}
