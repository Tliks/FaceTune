namespace Aoyon.FaceTune.Build;

/// <summary>Componentを後続Passが扱う標準形に揃える。</summary>
internal sealed class CanonicalizeComponentsPass : FaceTunePass<CanonicalizeComponentsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.canonicalize-components";
    public override string DisplayName => "Canonicalize Components";

    protected override void Execute(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        MenuCanonicalizer.Canonicalize(context);
        EmptyConditionRemover.Remove(root);
    }
}

internal static class MenuCanonicalizer
{
    public static void Canonicalize(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        var definitions = ParameterResolver.ResolveMenus(root, validateForBuild: true);

        foreach (var definition in definitions)
            Apply(root, definition);

        var parameterDomains = context.RequireSettings().ParameterDomains;
        var groupedDefinitions = definitions.Where(definition => definition.GenerateParameterGroup);
        var groupsByParameter = groupedDefinitions.GroupBy(
            definition => definition.ParameterName,
            StringComparer.Ordinal);
        foreach (var group in groupsByParameter)
        {
            parameterDomains = parameterDomains.WithIntDomainOverride(
                group.Key,
                new IntParameterDomain(0, group.Count()));
        }
        var settings = context.RequireSettings();
        context.SetSettings(settings with { ParameterDomains = parameterDomains });
    }

    private static void Apply(GameObject root, ResolvedMenuDefinition definition)
    {
        if (definition.ExistingMenu != null)
        {
            ApplyResolvedValues(definition.ExistingMenu, definition);
            return;
        }

        var menuObject = new GameObject(definition.Source is SettingsComponent
            ? $"{definition.Source.name} (Expression Set Menu)"
            : definition.Source.name);
        var parent = definition.Source.transform.parent.DestroyedAsNull() ?? root.transform;
        menuObject.transform.SetParent(parent, false);
        var menu = menuObject.AddComponent<MenuComponent>();
        ApplyResolvedValues(menu, definition);

        switch (definition.Source)
        {
            case ExpressionComponent expression:
                menu.Menu = expression.DirectMenuSettings.Menu;
                if (menu.Menu.Icon.Mode == MenuIconSettings.Kind.ExpressionPreview
                    && menu.Menu.Icon.PreviewExpression == null)
                {
                    menu.Menu.Icon.PreviewExpression = expression.transform;
                }
                expression.DirectMenuSettings.GeneratedCondition = MenuCondition.Enabled(menu);
                break;
            case SettingsComponent settings:
                menu.Menu = settings.ExpressionSet.Menu;
                settings.HasCondition = true;
                if (settings.Condition.Cases.Count == 0)
                    settings.Condition.Cases.Add(new ConditionCase());
                foreach (var conditionCase in settings.Condition.Cases)
                    conditionCase.MenuConditions.Add(MenuCondition.Enabled(menu));
                break;
        }
    }

    private static void ApplyResolvedValues(
        MenuComponent menu,
        ResolvedMenuDefinition definition)
    {
        menu.MenuKind = definition.Kind;
        menu.UseExistingParameter = definition.UseExistingParameter;
        menu.GenerateParameterGroup = definition.GenerateParameterGroup;
        menu.GroupName = definition.GroupName;
        menu.ParameterName = definition.ParameterName;
        menu.Synced = definition.Synced;
        menu.Saved = definition.Saved;
        menu.DefaultValue = definition.DefaultValue;
        menu.SelectedValue = definition.SelectedValue;
    }

}

internal static class EmptyConditionRemover
{
    public static void Remove(GameObject root)
    {
        foreach (var expression in root.GetComponentsInChildren<ExpressionComponent>(true))
        {
            if (!expression.HasCondition
                || expression.Condition.Mode != ConditionSelection.Kind.Conditional)
            {
                continue;
            }

            RemoveEmptyCases(expression.Condition.Condition);
            expression.HasCondition = !expression.Condition.Condition.IsEmpty;
        }

        foreach (var settings in root.GetComponentsInChildren<SettingsComponent>(true))
        {
            if (!settings.HasCondition) continue;

            RemoveEmptyCases(settings.Condition);
            settings.HasCondition = !settings.Condition.IsEmpty;
        }

        foreach (var control in root.GetComponentsInChildren<AvatarControlComponent>(true))
        {
            if (control.Condition.Mode != ConditionSelection.Kind.Conditional) continue;

            RemoveEmptyCases(control.Condition.Condition);
            if (control.Condition.Condition.IsEmpty)
                Object.DestroyImmediate(control);
        }
    }

    private static void RemoveEmptyCases(Condition condition)
        => condition.Cases.RemoveAll(conditionCase => conditionCase.IsEmpty);
}
