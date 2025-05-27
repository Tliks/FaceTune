using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;

namespace com.aoyon.facetune.pass;

internal class NegotiateMAMenuItemPass : Pass<NegotiateMAMenuItemPass>
{
    public override string QualifiedName => "com.aoyon.facetune.negotiate-ma-menu-item";
    public override string DisplayName => "Negotiate MA Menu Item";

    protected override void Execute(BuildContext context)
    {
        var root = context.AvatarRootObject;
        var expressionComponents = root.GetComponentsInChildren<ExpressionComponentBase>(false);
        var usedParameterNames = new HashSet<string>();

        foreach (var expressionComponent in expressionComponents)
        {
            var menuItem = expressionComponent.GetComponentNullable<ModularAvatarMenuItem>();
            if (menuItem == null) continue;

            if (expressionComponent.TryGetComponent<CommonConditionComponent>(out var _)) continue;

            var (parameterName, parameterCondition) = platform.PlatformSupport.MenuItemAsCondition(root.transform, menuItem, usedParameterNames);
            if (parameterName == null) continue;

            var conditionComponent = expressionComponent.gameObject.EnsureComponent<ConditionComponent>();
            conditionComponent.ParameterConditions.Add(parameterCondition!);
            conditionComponent.ExpressionFromSelfOnly = true;
        }
    }

}
