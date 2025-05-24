using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.pass;

internal class NegotiateMAMenuItemPass : Pass<NegotiateMAMenuItemPass>
{
    public override string QualifiedName => "com.aoyon.facetune.negotiate-ma-menu-item";
    public override string DisplayName => "Negotiate MA Menu Item";

    protected override void Execute(BuildContext context)
    {
        var root = context.AvatarRootObject;
        var asConditionComponents = root.GetComponentsInChildren<MAMenuItemAsConditionComponent>(false);
        var usedParameterNames = new HashSet<string>();

        foreach (var asConditionComponent in asConditionComponents)
        {
            var menuItem = asConditionComponent.GetComponentNullable<ModularAvatarMenuItem>();
            if (menuItem == null) throw new Exception($"ModularAvatarMenuItem is not found on {asConditionComponent.gameObject.name}");

            var conditionComponent = asConditionComponent.gameObject.EnsureComponent<ConditionComponent>();
            var parameterName = platform.PlatformSupport.AssignParameterName(root.transform, menuItem, usedParameterNames);
            conditionComponent.ParameterConditions.Add(new ParameterCondition(parameterName, true));
        }
    }

}
