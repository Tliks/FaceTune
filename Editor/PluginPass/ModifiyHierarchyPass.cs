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

        // Add Condition Component Phase
        NegotiateMAMenuItem(context.AvatarRootObject);
        // PostProcess Phase
        NormalizeData(context.AvatarRootObject);
    }

    private void NegotiateMAMenuItem(GameObject root)
    {
        var expressionComponents = root.GetComponentsInChildren<ExpressionComponentBase>(true);
        var usedParameterNames = new HashSet<string>();

        foreach (var expressionComponent in expressionComponents)
        {
            if (!expressionComponent.TryGetComponent<ModularAvatarMenuItem>(out var menuItem)) continue;

            var (parameterName, parameterCondition) = platform.PlatformSupport.MenuItemAsCondition(root.transform, menuItem, usedParameterNames);
            if (parameterName == null) continue;

            var conditionComponent = expressionComponent.gameObject.EnsureComponent<ConditionComponent>();
            conditionComponent.ParameterConditions.Add(parameterCondition!);
            expressionComponent.ExpressionFromSelfOnly = true;
        }
    }

    private void NormalizeData(GameObject root)
    {
        // 単一の条件をPatternとして扱うことでデータを正規化する
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
