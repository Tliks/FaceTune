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
        var parameterPlan = context.RequireParameterPlan();

        ApplyParameterBindings(parameterPlan);
        ExpandExpressionSets(root, parameterPlan);
        ExpandDirectMenus(root, parameterPlan);

        var settings = context.RequireSettings();
        var parameterDomains = settings.ParameterDomains;
        foreach (var (name, domain) in parameterPlan.IntDomains)
            parameterDomains = parameterDomains.WithIntDomainOverride(name, domain);
        context.SetSettings(settings with { ParameterDomains = parameterDomains });
    }

    private static void ApplyParameterBindings(ParameterPlan parameterPlan)
    {
        foreach (var (source, binding) in parameterPlan.Bindings)
        {
            if (source is not MenuComponent menu) continue;
            ApplyParameterBinding(menu, binding);
        }
    }

    private static void ExpandExpressionSets(GameObject root, ParameterPlan parameterPlan)
    {
        var allSettings = root.GetComponentsInChildren<SettingsComponent>(true);
        var expressionSets = allSettings.Where(settings => settings.ExpressionSetEnabled).ToArray();

        foreach (var settings in expressionSets)
        {
            var menuObject = new GameObject($"{settings.name} (Expression Set Menu)");
            var parent = settings.transform.parent.DestroyedAsNull() ?? root.transform;
            menuObject.transform.SetParent(parent, false);
            menuObject.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);

            var menu = menuObject.AddComponent<MenuComponent>();
            menu.MenuKind = MenuComponent.Kind.Toggle;
            menu.Menu = settings.ExpressionSet.Menu;
            menu.UseExistingParameter = false;
            menu.DefaultValue = settings.ExpressionSet.DefaultSelected ? 1f : 0f;
            ApplyParameterBinding(menu, parameterPlan.GetBinding(settings));

            settings.HasCondition = true;
            if (settings.Condition.Cases.Count == 0)
                settings.Condition.Cases.Add(new ConditionCase());

            foreach (var conditionCase in settings.Condition.Cases)
                conditionCase.MenuConditions.Add(MenuCondition.Enabled(menu));
        }
    }

    private static void ExpandDirectMenus(GameObject root, ParameterPlan parameterPlan)
    {
        var expressions = root.GetComponentsInChildren<ExpressionComponent>(true);
        var sources = expressions.Where(expression => expression.DirectMenuEnabled).ToArray();

        foreach (var source in sources)
        {
            var menuObject = new GameObject(source.name);
            var parent = source.transform.parent.DestroyedAsNull() ?? root.transform;
            menuObject.transform.SetParent(parent, false);
            menuObject.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            source.DirectMenuSettings.GeneratedCondition = MenuCondition.Enabled(
                CreateDirectMenu(
                    menuObject,
                    source,
                    parameterPlan.GetBinding(source)));
        }
    }

    private static MenuComponent CreateDirectMenu(
        GameObject menuObject,
        ExpressionComponent source,
        ParameterBinding binding)
    {
        var menu = menuObject.AddComponent<MenuComponent>();
        menu.MenuKind = MenuComponent.Kind.Toggle;
        menu.Menu = source.DirectMenuSettings.Menu;
        if (menu.Menu.Icon.Mode == MenuIconSettings.Kind.ExpressionPreview
            && menu.Menu.Icon.PreviewExpression == null)
        {
            menu.Menu.Icon.PreviewExpression = source.transform;
        }
        menu.UseExistingParameter = false;
        ApplyParameterBinding(menu, binding);
        return menu;
    }

    private static void ApplyParameterBinding(MenuComponent menu, ParameterBinding binding)
    {
        menu.ParameterName = binding.Name;
        menu.SelectedValue = binding.SelectedValue;
        menu.GenerateParameterGroup = binding.GenerateParameterGroup;
        menu.GroupName = binding.GroupName;
        menu.Synced = binding.Synced;
        menu.Saved = binding.Saved;
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
