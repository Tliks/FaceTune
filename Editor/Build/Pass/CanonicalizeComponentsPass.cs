namespace Aoyon.FaceTune.Build;

/// <summary>Componentを後続Passが扱う標準形に揃える。</summary>
internal sealed class CanonicalizeComponentsPass : FaceTunePass<CanonicalizeComponentsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.canonicalize-components";
    public override string DisplayName => "Canonicalize Components";

    protected override void Execute(FaceTuneContext context)
    {
        MenuCanonicalizer.Canonicalize(context);
        EmptyConditionRemover.Remove(context.AvatarContext.Root);
    }
}

internal static class MenuCanonicalizer
{
    public static void Canonicalize(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;

        ExpandExpressionSets(root);
        var directMenus = ExpandDirectMenus(root);

        var settings = context.RequireSettings();
        var parameterDomains = BindParameters(root, settings.ParameterDomains);
        context.SetSettings(settings with { ParameterDomains = parameterDomains });

        LowerMenuReferences(root, directMenus);
        ApplyInstallOverrides(root);
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

    private static IReadOnlyList<(DirectMenuSettings Settings, MenuComponent Menu)> ExpandDirectMenus(
        GameObject root)
    {
        var sources = root.GetComponentsInChildren<ExpressionComponent>(true)
            .Where(expression => expression.DirectMenuEnabled)
            .ToArray();
        var result = new List<(DirectMenuSettings Settings, MenuComponent Menu)>();

        foreach (var source in sources)
        {
            var menuObject = new GameObject($"{source.name} (Direct Menu)");
            var parent = source.transform.parent.DestroyedAsNull() ?? root.transform;
            menuObject.transform.SetParent(parent, false);
            result.Add((source.DirectMenuSettings, CreateDirectMenu(menuObject, source)));
        }

        return result;
    }

    private static MenuComponent CreateDirectMenu(GameObject menuObject, ExpressionComponent source)
    {
        var menu = menuObject.AddComponent<MenuComponent>();
        menu.MenuKind = MenuComponent.Kind.Toggle;
        menu.Menu = source.DirectMenuSettings.Menu;
        menu.UseExistingParameter = false;
        menu.GenerateParameterGroup = source.WriteMode == ExpressionWriteMode.Replace
            || !string.IsNullOrWhiteSpace(source.DirectMenuSettings.GroupName);
        menu.GroupName = source.WriteMode == ExpressionWriteMode.Replace
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

    private static void ApplyInstallOverrides(GameObject root)
    {
        foreach (var menu in root.GetComponentsInChildren<MenuComponent>(true))
        {
            var target = menu.Menu.InstallContainer;
            if (target == null) continue;

            if (target != root.transform && !target.IsChildOf(root.transform))
            {
                throw new InvalidOperationException(
                    $"Menu install target is outside the avatar: '{menu.name}'.");
            }

            if (target == menu.transform || target.IsChildOf(menu.transform))
            {
                throw new InvalidOperationException(
                    $"Menu install target creates a hierarchy cycle: '{menu.name}'.");
            }

            menu.transform.SetParent(target, false);
            menu.Menu.InstallContainer = null;
        }
    }

    private static void LowerMenuReferences(
        GameObject root,
        IEnumerable<(DirectMenuSettings Settings, MenuComponent Menu)> directMenus)
    {
        LowerMultiFrameMenuReferences(root);
        LowerMenuConditions(root);
        foreach (var (settings, menu) in directMenus)
            settings.GeneratedCondition = LowerMenuCondition(MenuCondition.Enabled(menu));
    }

    private static void LowerMultiFrameMenuReferences(GameObject root)
    {
        foreach (var expression in root.GetComponentsInChildren<ExpressionComponent>(true))
        {
            var settings = expression.MultiFrame;
            if (settings.MultiFrameMode != MultiFrameSettings.Kind.Menu) continue;
            if (settings.MenuSource == null)
            {
                settings.MultiFrameMode = MultiFrameSettings.Kind.Default;
                continue;
            }

            settings.MultiFrameMode = MultiFrameSettings.Kind.Parameter;
            settings.ParameterName = settings.MenuSource.ParameterName;
            settings.MenuSource = null;
        }
    }

    private static void LowerMenuConditions(GameObject root)
    {
        var sources = root.GetComponentsInChildren<FaceTuneTagComponent>(true)
            .OfType<IHasConditions>();
        foreach (var source in sources)
        {
            foreach (var condition in source.Conditions)
            {
                foreach (var conditionCase in condition.Cases)
                {
                    var parameterConditions = conditionCase.MenuConditions
                        .Where(menuCondition => menuCondition.MenuSource != null)
                        .Select(LowerMenuCondition);
                    conditionCase.ParameterConditions.AddRange(parameterConditions);
                    conditionCase.MenuConditions.Clear();
                }
            }
        }
    }

    private static ParameterCondition LowerMenuCondition(MenuCondition condition)
    {
        var menu = condition.MenuSource!;
        if (menu.MenuKind == MenuComponent.Kind.Radial)
        {
            if (condition.Mode is not (MenuConditionMode.LessThan or MenuConditionMode.GreaterThan))
            {
                throw new InvalidOperationException(
                    $"Radial menu '{menu.name}' requires a radial condition.");
            }
            return ParameterCondition.Float(
                menu.ParameterName,
                (ComparisonType)condition.Mode,
                condition.Threshold);
        }

        if (menu.MenuKind != MenuComponent.Kind.Toggle
            || condition.Mode is not (MenuConditionMode.Enabled or MenuConditionMode.Disabled))
        {
            throw new InvalidOperationException(
                $"Menu '{menu.name}' has an incompatible condition.");
        }
        var isEnabled = condition.Mode == MenuConditionMode.Enabled;
        return !menu.UseExistingParameter && menu.GenerateParameterGroup
            ? ParameterCondition.Int(
                menu.ParameterName,
                isEnabled ? ComparisonType.Equal : ComparisonType.NotEqual,
                (int)menu.SelectedValue)
            : ParameterCondition.Bool(
                menu.ParameterName,
                isEnabled == (menu.SelectedValue != 0f));
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
