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

        ExpandExpressionSets(root);
        var behavior = new ExpressionBehaviorResolver();
        ExpandDirectMenus(root, behavior);

        var settings = context.RequireSettings();
        var parameterDomains = BindParameters(root, settings.ParameterDomains);
        context.SetSettings(settings with { ParameterDomains = parameterDomains });
    }

    private static void ExpandExpressionSets(GameObject root)
    {
        var expressionSets = root.GetComponentsInChildren<SettingsComponent>(true)
            .Where(settings => settings.ExpressionSetEnabled)
            .ToArray();

        foreach (var settings in expressionSets)
        {
            var menuObject = new GameObject($"{settings.name} (Expression Set Menu)");
            var parent = settings.transform.parent.DestroyedAsNull() ?? root.transform;
            menuObject.transform.SetParent(parent, false);

            var menu = menuObject.AddComponent<MenuComponent>();
            menu.MenuKind = MenuComponent.Kind.Toggle;
            menu.Menu = settings.ExpressionSet.Menu;
            menu.UseExistingParameter = false;
            menu.GenerateParameterGroup = true;
            menu.GroupName = BuiltInMenuGroups.ExpressionSet;
            menu.Synced = true;
            menu.Saved = true;
            menu.DefaultValue = settings.ExpressionSet.DefaultSelected ? 1f : 0f;
            menu.SelectedValue = 1f;

            settings.HasCondition = true;
            if (settings.Condition.Cases.Count == 0)
                settings.Condition.Cases.Add(new ConditionCase());

            foreach (var conditionCase in settings.Condition.Cases)
                conditionCase.MenuConditions.Add(MenuCondition.Enabled(menu));
        }
    }

    private static void ExpandDirectMenus(
        GameObject root,
        ExpressionBehaviorResolver behavior)
    {
        var sources = root.GetComponentsInChildren<ExpressionComponent>(true)
            .Where(expression => expression.DirectMenuEnabled)
            .ToArray();

        foreach (var source in sources)
        {
            var menuObject = new GameObject(source.name);
            var parent = source.transform.parent.DestroyedAsNull() ?? root.transform;
            menuObject.transform.SetParent(parent, false);
            source.DirectMenuSettings.GeneratedCondition = MenuCondition.Enabled(
                CreateDirectMenu(menuObject, source, behavior.Resolve(source)));
        }
    }

    private static MenuComponent CreateDirectMenu(
        GameObject menuObject,
        ExpressionComponent source,
        ExpressionBehavior behavior)
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
        var writeMode = behavior.WriteMode;
        menu.GenerateParameterGroup = writeMode == ExpressionWriteMode.Replace
            || !string.IsNullOrWhiteSpace(source.DirectMenuSettings.GroupName);
        menu.GroupName = writeMode == ExpressionWriteMode.Replace
            ? BuiltInMenuGroups.DirectMenuReplace
            : source.DirectMenuSettings.GroupName;
        menu.Synced = true;
        menu.Saved = true;
        return menu;
    }

    private static ParameterDomainRegistry BindParameters(
        GameObject root,
        ParameterDomainRegistry parameterDomains)
    {
        var menus = root.GetComponentsInChildren<MenuComponent>(true);
        var configuredNames = new HashSet<string>(StringComparer.Ordinal);
        NormalizeParameterGroups(menus);
        BindIndividualParameters(root, menus, configuredNames);
        return BindParameterGroups(menus, configuredNames, parameterDomains);
    }

    private static void NormalizeParameterGroups(IEnumerable<MenuComponent> menus)
    {
        foreach (var menu in menus)
        {
            var canUseGroup = !menu.UseExistingParameter
                && menu.MenuKind == MenuComponent.Kind.Toggle
                && !string.IsNullOrWhiteSpace(menu.GroupName);
            if (!canUseGroup)
            {
                menu.GenerateParameterGroup = false;
            }
            else if (menu.GenerateParameterGroup)
            {
                menu.Synced = true;
                menu.Saved = true;
            }
        }
    }

    private static void BindIndividualParameters(
        GameObject root,
        IEnumerable<MenuComponent> menus,
        ISet<string> configuredNames)
    {
        var individualMenus = menus.Where(menu =>
            menu.MenuKind != MenuComponent.Kind.Folder
            && !IsGeneratedGroup(menu));

        foreach (var menu in individualMenus)
        {
            if (menu.UseExistingParameter)
            {
                ValidateParameterName(menu.ParameterName, menu);
                continue;
            }

            if (string.IsNullOrWhiteSpace(menu.ParameterName))
            {
                menu.ParameterName = GenerateAutomaticParameterName(root, menu);
                continue;
            }

            ValidateParameterName(menu.ParameterName, menu);
            if (!configuredNames.Add(menu.ParameterName))
            {
                throw new InvalidOperationException(
                    $"Menu parameter name is used by multiple generated controls: '{menu.ParameterName}'.");
            }
        }
    }

    private static ParameterDomainRegistry BindParameterGroups(
        IEnumerable<MenuComponent> menus,
        ISet<string> configuredNames,
        ParameterDomainRegistry parameterDomains)
    {
        var groups = menus
            .Where(IsGeneratedGroup)
            .GroupBy(menu => menu.GroupName, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var options = group.ToArray();
            var parameterName = $"{FaceTuneConstants.GeneratedParameterPrefix}/MenuGroup/{group.Key}";
            ValidateParameterGroup(group.Key, parameterName, options[0]);
            if (!configuredNames.Add(parameterName))
            {
                throw new InvalidOperationException(
                    $"Menu parameter name conflicts with a generated group: '{parameterName}'.");
            }

            if (options.Count(menu => menu.DefaultValue != 0f) > 1)
            {
                throw new InvalidOperationException(
                    $"Menu group '{group.Key}' has multiple initial options.");
            }

            for (var index = 0; index < options.Length; index++)
            {
                options[index].ParameterName = parameterName;
                options[index].SelectedValue = index + 1;
            }
            parameterDomains = parameterDomains.WithIntDomainOverride(
                parameterName,
                new IntParameterDomain(0, options.Length));
        }

        return parameterDomains;
    }

    private static bool IsGeneratedGroup(MenuComponent menu)
        => !menu.UseExistingParameter && menu.GenerateParameterGroup;

    private static string GenerateAutomaticParameterName(GameObject root, MenuComponent menu)
    {
        var indices = new Stack<int>();
        for (var current = menu.transform; current != root.transform; current = current.parent!)
            indices.Push(current.GetSiblingIndex());
        var pathHash = Hash128.Compute(string.Join("/", indices));
        return $"{FaceTuneConstants.GeneratedParameterPrefix}/Menu/{menu.gameObject.name}_{pathHash}";
    }

    private static void ValidateParameterGroup(
        string groupName,
        string parameterName,
        MenuComponent menu)
    {
        if (string.IsNullOrWhiteSpace(groupName) || groupName.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"Menu group name is invalid: '{menu.name}'.");
        }

        ValidateParameterName(parameterName, menu);
    }

    private static void ValidateParameterName(string name, MenuComponent menu)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Length > 256
            || name.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"Menu parameter name is invalid: '{menu.name}'.");
        }
    }
}

internal static class EmptyConditionRemover
{
    public static void Remove(GameObject root)
    {
        var sources = root.GetComponentsInChildren<FaceTuneTagComponent>(true)
            .OfType<IHasConditions>();

        foreach (var source in sources)
        {
            foreach (var condition in source.Conditions)
                condition.Cases.RemoveAll(conditionCase => conditionCase.IsEmpty);

            switch (source)
            {
                case ExpressionComponent expression when HasEmptyCondition(expression):
                    expression.HasCondition = false;
                    break;

                case SettingsComponent settings when HasEmptyCondition(settings):
                    settings.HasCondition = false;
                    break;

                case AvatarControlComponent control when HasEmptyCondition(control):
                    Object.DestroyImmediate(control);
                    break;
            }
        }
    }

    private static bool HasEmptyCondition(ExpressionComponent expression)
        => expression.HasCondition
           && expression.Condition.Mode == ConditionSelection.Kind.Conditional
           && expression.Condition.Condition.IsEmpty;

    private static bool HasEmptyCondition(SettingsComponent settings)
        => settings.HasCondition && settings.Condition.IsEmpty;

    private static bool HasEmptyCondition(AvatarControlComponent control)
        => control.Condition.Mode == ConditionSelection.Kind.Conditional
           && control.Condition.Condition.IsEmpty;
}
