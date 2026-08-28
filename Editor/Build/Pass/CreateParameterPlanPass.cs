namespace Aoyon.FaceTune.Build;

internal sealed class CreateParameterPlanPass : FaceTunePass<CreateParameterPlanPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.create-parameter-plan";
    public override string DisplayName => "Create Parameter Plan";

    protected override void Execute(FaceTuneContext context)
    {
        context.SetParameterPlan(ParameterPlanBuilder.Build(context.AvatarContext.Root));
    }
}

internal static class ParameterPlanBuilder
{
    public static ParameterPlan Build(GameObject root)
    {
        var menus = root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.MenuKind != MenuComponent.Kind.Folder)
            .Where(menu => !menu.UseExistingParameter)
            .ToArray();

        var individualParameters = menus
            .Where(menu => !menu.GenerateParameterGroup)
            .Select(BuildItem);

        var groupParameters = menus
            .Where(menu => menu.GenerateParameterGroup)
            .GroupBy(menu => menu.ParameterName, StringComparer.Ordinal)
            .Select(BuildGroupItem);

        return new ParameterPlan(individualParameters.Concat(groupParameters));
    }

    private static ParameterItem BuildItem(MenuComponent menu)
    {
        var type = menu.MenuKind == MenuComponent.Kind.Toggle
            ? ParameterValueType.Bool
            : ParameterValueType.Float;

        return new ParameterItem(
            menu.ParameterName,
            type,
            GetParameterDefaultValue(menu),
            menu.Synced,
            menu.Saved);
    }

    private static ParameterItem BuildGroupItem(IGrouping<string, MenuComponent> group)
    {
        var defaultMenu = group
            .FirstOrDefault(menu => menu.DefaultValue != 0f)
            .DestroyedAsNull();
        var defaultValue = defaultMenu?.SelectedValue ?? 0f;

        return new ParameterItem(
            group.Key,
            ParameterValueType.Int,
            defaultValue,
            Synced: true,
            Saved: true);
    }

    private static float GetParameterDefaultValue(MenuComponent menu)
    {
        if (menu.MenuKind != MenuComponent.Kind.Toggle)
        {
            return menu.DefaultValue;
        }

        var defaultSelected = menu.DefaultValue != 0f;
        var selectedValueIsTrue = menu.SelectedValue != 0f;
        return defaultSelected == selectedValueIsTrue ? 1f : 0f;
    }
}
