using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune;

internal static class ParameterResolver
{
    public static ParameterPlan Resolve(GameObject root, bool validateForBuild = false)
    {
        var sources = ResolveSources(root);
        if (validateForBuild) Validate(root, sources);
        var items = new List<ParameterItem>();
        var individualSources = sources.Where(source => !source.GenerateParameterGroup);
        foreach (var source in individualSources)
        {
            items.Add(new ParameterItem(
                source.Source,
                source.ParameterName,
                source.Kind == MenuComponent.Kind.Toggle
                    ? ParameterValueType.Bool
                    : ParameterValueType.Float,
                GetDefaultValue(source),
                source.Synced,
                source.Saved));
        }

        var groupedSources = sources.Where(source => source.GenerateParameterGroup);
        var groups = groupedSources.GroupBy(source => source.ParameterName, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var members = group.ToArray();
            var defaultSource = members.FirstOrDefault(source => source.DefaultValue != 0f);
            items.Add(new ParameterItem(
                members[0].Source,
                group.Key,
                ParameterValueType.Int,
                defaultSource?.SelectedValue ?? 0f,
                Synced: true,
                Saved: true));
        }

        var bindings = sources.ToDictionary(
            source => source.Source,
            source => new ParameterBinding(
                source.ParameterName,
                source.SelectedValue,
                source.GenerateParameterGroup,
                source.GroupName,
                source.Synced,
                source.Saved));
        var intDomains = groups.ToDictionary(
            group => group.Key,
            group => new IntParameterDomain(0, group.Count()),
            StringComparer.Ordinal);
        return new ParameterPlan(items, bindings, intDomains);
    }

    public static string GenerateParameterName(
        GameObject root,
        FaceTuneTagComponent source,
        string purpose)
    {
        var indices = new Stack<int>();
        for (var current = source.transform; current != root.transform; current = current.parent!)
            indices.Push(current.GetSiblingIndex());
        var componentIndex = Array.IndexOf(source.GetComponents<FaceTuneTagComponent>(), source);
        var identity = $"{string.Join("/", indices)}:{componentIndex}:{purpose}";
        var hash = Hash128.Compute(identity);
        return $"{FaceTuneConstants.GeneratedParameterPrefix}/Menu/{source.gameObject.name}_{hash}";
    }

    public static string GetGroupParameterName(string groupName)
        => $"{FaceTuneConstants.GeneratedParameterPrefix}/MenuGroup/{groupName}";

    private static IReadOnlyList<ParameterSource> ResolveSources(GameObject root)
    {
        var result = new List<ParameterSource>();
        AddMenus(root, result);
        AddDirectMenus(root, result);
        AddPresets(root, result);
        AssignParameterGroups(result);
        return result;
    }

    private static void AddMenus(GameObject root, ICollection<ParameterSource> result)
    {
        var components = root.GetComponentsInChildren<MenuComponent>(true);
        foreach (var menu in components)
        {
            if (menu.MenuKind == MenuComponent.Kind.Folder || menu.UseExistingParameter) continue;
            var grouped = menu.MenuKind == MenuComponent.Kind.Toggle
                          && menu.GenerateParameterGroup
                          && !string.IsNullOrWhiteSpace(menu.GroupName);
            var parameterName = string.IsNullOrWhiteSpace(menu.ParameterName)
                ? GenerateParameterName(root, menu, "Menu")
                : menu.ParameterName;
            result.Add(new ParameterSource(
                menu,
                menu.MenuKind,
                grouped,
                menu.GroupName,
                parameterName,
                grouped || menu.Synced,
                grouped || menu.Saved,
                menu.DefaultValue,
                menu.SelectedValue));
        }
    }

    private static void AddDirectMenus(GameObject root, ICollection<ParameterSource> result)
    {
        var behavior = new ExpressionBehaviorResolver();
        var expressions = root.GetComponentsInChildren<ExpressionComponent>(true);
        foreach (var expression in expressions)
        {
            if (!expression.DirectMenuEnabled) continue;
            var writeMode = behavior.Resolve(expression).WriteMode;
            var groupName = writeMode == ExpressionWriteMode.Replace
                ? BuiltInMenuGroups.DirectMenuReplace
                : expression.DirectMenuSettings.GroupName;
            var grouped = writeMode == ExpressionWriteMode.Replace
                          || !string.IsNullOrWhiteSpace(groupName);
            var parameterName = grouped
                ? string.Empty
                : GenerateParameterName(root, expression, "DirectMenu");
            result.Add(new ParameterSource(
                expression,
                MenuComponent.Kind.Toggle,
                grouped,
                groupName,
                parameterName,
                true,
                true,
                0f,
                1f));
        }
    }

    private static void AddPresets(GameObject root, ICollection<ParameterSource> result)
    {
        var settingsComponents = root.GetComponentsInChildren<SettingsComponent>(true);
        foreach (var settings in settingsComponents)
        {
            if (!settings.ExpressionSetEnabled) continue;
            result.Add(new ParameterSource(
                settings,
                MenuComponent.Kind.Toggle,
                true,
                BuiltInMenuGroups.ExpressionSet,
                string.Empty,
                true,
                true,
                settings.ExpressionSet.DefaultSelected ? 1f : 0f,
                1f));
        }
    }

    private static void AssignParameterGroups(IReadOnlyList<ParameterSource> sources)
    {
        var groupedSources = sources.Where(source => source.GenerateParameterGroup);
        var groups = groupedSources.GroupBy(source => source.GroupName, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var parameterName = GetGroupParameterName(group.Key);
            var selectedValue = 1f;
            foreach (var source in group)
            {
                source.ParameterName = parameterName;
                source.SelectedValue = selectedValue++;
            }
        }
    }

    private static void Validate(GameObject root, IReadOnlyList<ParameterSource> sources)
    {
        var menus = root.GetComponentsInChildren<MenuComponent>(true);
        var existingParameters = menus.Where(menu =>
            menu.MenuKind != MenuComponent.Kind.Folder && menu.UseExistingParameter);
        foreach (var menu in existingParameters)
            ValidateParameterName(menu.ParameterName, menu);

        foreach (var source in sources)
        {
            ValidateParameterName(source.ParameterName, source.Source);
            if (source.GenerateParameterGroup
                && (string.IsNullOrWhiteSpace(source.GroupName)
                    || source.GroupName.Any(char.IsControl)))
            {
                throw new InvalidOperationException(
                    $"Menu group name is invalid: '{source.Source.name}'.");
            }
        }

        var sourcesByName = sources.GroupBy(source => source.ParameterName, StringComparer.Ordinal);
        foreach (var sameName in sourcesByName)
        {
            if (sameName.Count() > 1 && sameName.Any(source => !source.GenerateParameterGroup))
            {
                throw new InvalidOperationException(
                    $"Menu parameter name is used by multiple generated controls: '{sameName.Key}'.");
            }
        }

        var groupedSources = sources.Where(source => source.GenerateParameterGroup);
        var groups = groupedSources.GroupBy(source => source.ParameterName, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            if (group.Count(source => source.DefaultValue != 0f) > 1)
            {
                throw new InvalidOperationException(
                    $"Menu group '{group.First().GroupName}' has multiple initial options.");
            }
        }
    }

    private static void ValidateParameterName(string name, FaceTuneTagComponent source)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Length > 256
            || name.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"Menu parameter name is invalid: '{source.name}'.");
        }
    }

    private static float GetDefaultValue(ParameterSource source)
    {
        if (source.Kind != MenuComponent.Kind.Toggle) return source.DefaultValue;
        return (source.DefaultValue != 0f) == (source.SelectedValue != 0f) ? 1f : 0f;
    }

    private sealed class ParameterSource
    {
        public FaceTuneTagComponent Source { get; }
        public MenuComponent.Kind Kind { get; }
        public bool GenerateParameterGroup { get; }
        public string GroupName { get; }
        public string ParameterName { get; set; }
        public bool Synced { get; }
        public bool Saved { get; }
        public float DefaultValue { get; }
        public float SelectedValue { get; set; }

        public ParameterSource(
            FaceTuneTagComponent source,
            MenuComponent.Kind kind,
            bool generateParameterGroup,
            string groupName,
            string parameterName,
            bool synced,
            bool saved,
            float defaultValue,
            float selectedValue)
        {
            Source = source;
            Kind = kind;
            GenerateParameterGroup = generateParameterGroup;
            GroupName = groupName;
            ParameterName = parameterName;
            Synced = synced;
            Saved = saved;
            DefaultValue = defaultValue;
            SelectedValue = selectedValue;
        }
    }
}
