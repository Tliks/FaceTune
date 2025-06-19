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
        var expressionComponents = passContext.BuildContext.AvatarRootObject.GetComponentsInChildren<ExpressionComponentBase>(true);
        var usedParameterNames = new HashSet<string>();

        foreach (var expressionComponent in expressionComponents)
        {
            if (!expressionComponent.TryGetComponent<ModularAvatarMenuItem>(out var menuItem)) continue;

            var (parameterName, parameterCondition) = passContext.PlatformSupport.MenuItemAsCondition(menuItem, usedParameterNames);
            if (parameterName == null) continue;

            var conditionComponent = expressionComponent.gameObject.AddComponent<ConditionComponent>(); // OR
            conditionComponent.ParameterConditions.Add(parameterCondition!);
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
