using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune;

internal sealed record ParameterDeclaration(
    FaceTuneTagComponent Source,
    string Name,
    ParameterValueType Type,
    float DefaultValue,
    bool Synced,
    bool Saved);

internal sealed record ResolvedMenuDefinition(
    FaceTuneTagComponent Source,
    MenuComponent? ExistingMenu,
    MenuComponent.Kind Kind,
    bool UseExistingParameter,
    bool GenerateParameterGroup,
    string GroupName,
    string ParameterName,
    bool Synced,
    bool Saved,
    float DefaultValue,
    float SelectedValue);

internal static class ParameterResolver
{
    public static IReadOnlyList<ResolvedMenuDefinition> ResolveMenus(
        GameObject root,
        bool validateForBuild = false)
    {
        var unresolved = CollectMenus(root);
        var groupIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<ResolvedMenuDefinition>(unresolved.Count);
        foreach (var menu in unresolved)
        {
            if (menu.GenerateParameterGroup)
            {
                var selectedValue = groupIndices.TryGetValue(menu.GroupName, out var previous)
                    ? previous + 1
                    : 1;
                groupIndices[menu.GroupName] = selectedValue;
                result.Add(menu.Resolve(GroupParameterName(menu.GroupName), selectedValue));
                continue;
            }

            result.Add(menu.Resolve(
                menu.UseExistingParameter || !string.IsNullOrWhiteSpace(menu.ParameterName)
                    ? menu.ParameterName
                    : GeneratedParameterName(root, menu.Source, menu.Purpose),
                menu.SelectedValue));
        }
        if (validateForBuild) Validate(result);
        return result;
    }

    public static IReadOnlyList<ParameterDeclaration> ResolveParameters(GameObject root)
    {
        var menus = ResolveMenus(root).Where(menu => !menu.UseExistingParameter).ToArray();
        var individualMenus = menus.Where(menu => !menu.GenerateParameterGroup);
        var result = individualMenus.Select(menu => new ParameterDeclaration(
            menu.Source,
            menu.ParameterName,
            menu.Kind == MenuComponent.Kind.Toggle
                ? ParameterValueType.Bool
                : ParameterValueType.Float,
            GetIndividualDefaultValue(menu),
            menu.Synced,
            menu.Saved)).ToList();

        var groupedMenus = menus.Where(menu => menu.GenerateParameterGroup);
        var groupsByParameter = groupedMenus.GroupBy(
            menu => menu.ParameterName,
            StringComparer.Ordinal);
        foreach (var group in groupsByParameter)
        {
            var members = group.ToArray();
            var defaultMenu = members.FirstOrDefault(menu => menu.DefaultValue != 0f);
            result.Add(new ParameterDeclaration(
                members[0].Source,
                group.Key,
                ParameterValueType.Int,
                defaultMenu?.SelectedValue ?? 0f,
                Synced: true,
                Saved: true));
        }

        return result;
    }

    public static string GroupParameterName(string groupName)
        => $"{FaceTuneConstants.GeneratedParameterPrefix}/MenuGroup/{groupName}";

    private static List<UnresolvedMenuDefinition> CollectMenus(GameObject root)
    {
        var result = new List<UnresolvedMenuDefinition>();
        var behavior = new ExpressionBehaviorResolver();
        foreach (var component in root.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            switch (component)
            {
                case MenuComponent menu when menu.MenuKind != MenuComponent.Kind.Folder:
                {
                    var grouped = !menu.UseExistingParameter
                                  && menu.MenuKind == MenuComponent.Kind.Toggle
                                  && menu.GenerateParameterGroup
                                  && !string.IsNullOrWhiteSpace(menu.GroupName);
                    result.Add(new UnresolvedMenuDefinition(
                        menu,
                        menu,
                        menu.MenuKind,
                        menu.UseExistingParameter,
                        grouped,
                        menu.GroupName,
                        menu.ParameterName,
                        "Menu",
                        grouped || menu.Synced,
                        grouped || menu.Saved,
                        menu.DefaultValue,
                        menu.SelectedValue));
                    break;
                }
                case ExpressionComponent expression when expression.DirectMenuEnabled:
                {
                    var settings = expression.DirectMenuSettings;
                    var writeMode = behavior.Resolve(expression).WriteMode;
                    var grouped = writeMode == ExpressionWriteMode.Replace
                                  || !string.IsNullOrWhiteSpace(settings.GroupName);
                    result.Add(new UnresolvedMenuDefinition(
                        expression,
                        null,
                        MenuComponent.Kind.Toggle,
                        UseExistingParameter: false,
                        grouped,
                        writeMode == ExpressionWriteMode.Replace
                            ? BuiltInMenuGroups.DirectMenuReplace
                            : settings.GroupName,
                        string.Empty,
                        "DirectMenu",
                        Synced: true,
                        Saved: true,
                        DefaultValue: 0f,
                        SelectedValue: 1f));
                    break;
                }
                case SettingsComponent settings when settings.ExpressionSetEnabled:
                    result.Add(new UnresolvedMenuDefinition(
                        settings,
                        null,
                        MenuComponent.Kind.Toggle,
                        UseExistingParameter: false,
                        GenerateParameterGroup: true,
                        BuiltInMenuGroups.ExpressionSet,
                        string.Empty,
                        "ExpressionSet",
                        Synced: true,
                        Saved: true,
                        settings.ExpressionSet.DefaultSelected ? 1f : 0f,
                        SelectedValue: 1f));
                    break;
            }
        }

        var virtualSiblingIndices = new Dictionary<FaceTuneTagComponent, int>();
        var virtualMenus = result.Where(menu => menu.ExistingMenu == null).ToArray();
        foreach (var parentGroup in virtualMenus.GroupBy(menu =>
                     menu.Source.transform.parent.DestroyedAsNull() ?? root.transform))
        {
            var nextIndex = parentGroup.Key.childCount;
            foreach (var menu in parentGroup.Where(menu => menu.Source is SettingsComponent))
                virtualSiblingIndices.Add(menu.Source, nextIndex++);
            foreach (var menu in parentGroup.Where(menu => menu.Source is ExpressionComponent))
                virtualSiblingIndices.Add(menu.Source, nextIndex++);
        }

        result.Sort((left, right) => CompareHierarchyOrder(
            GetCanonicalPath(root, left, virtualSiblingIndices),
            GetCanonicalPath(root, right, virtualSiblingIndices)));
        return result;
    }

    private static int[] GetCanonicalPath(
        GameObject root,
        UnresolvedMenuDefinition menu,
        IReadOnlyDictionary<FaceTuneTagComponent, int> virtualSiblingIndices)
    {
        var transform = menu.ExistingMenu != null
            ? menu.ExistingMenu.transform
            : menu.Source.transform.parent.DestroyedAsNull() ?? root.transform;
        var indices = new Stack<int>();
        for (var current = transform; current != root.transform; current = current.parent!)
            indices.Push(current.GetSiblingIndex());
        var path = indices.ToList();
        if (menu.ExistingMenu == null)
            path.Add(virtualSiblingIndices[menu.Source]);
        return path.ToArray();
    }

    private static int CompareHierarchyOrder(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        var count = Mathf.Min(left.Count, right.Count);
        for (var index = 0; index < count; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0) return comparison;
        }
        return left.Count.CompareTo(right.Count);
    }

    private static void Validate(IReadOnlyList<ResolvedMenuDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            ValidateParameterName(definition.ParameterName, definition.Source);
            if (definition.GenerateParameterGroup
                && (string.IsNullOrWhiteSpace(definition.GroupName)
                    || definition.GroupName.Any(char.IsControl)))
            {
                throw new InvalidOperationException(
                    $"Menu group name is invalid: '{definition.Source.name}'.");
            }
        }

        var generatedDefinitions = definitions.Where(definition => !definition.UseExistingParameter);
        var definitionsByName = generatedDefinitions.GroupBy(
            definition => definition.ParameterName,
            StringComparer.Ordinal);
        foreach (var sameName in definitionsByName)
        {
            if (sameName.Count() > 1 && sameName.Any(definition => !definition.GenerateParameterGroup))
            {
                throw new InvalidOperationException(
                    $"Menu parameter name is used by multiple generated controls: '{sameName.Key}'.");
            }
        }

        var groupedDefinitions = definitions.Where(definition => definition.GenerateParameterGroup);
        var groupsByParameter = groupedDefinitions.GroupBy(
            definition => definition.ParameterName,
            StringComparer.Ordinal);
        foreach (var group in groupsByParameter)
        {
            if (group.Count(definition => definition.DefaultValue != 0f) > 1)
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

    private static string GeneratedParameterName(
        GameObject root,
        FaceTuneTagComponent source,
        string purpose)
    {
        var indices = new Stack<int>();
        for (var current = source.transform; current != root.transform; current = current.parent!)
            indices.Push(current.GetSiblingIndex());
        var componentIndex = Array.IndexOf(source.GetComponents<FaceTuneTagComponent>(), source);
        var identity = $"{string.Join("/", indices)}:{componentIndex}:{purpose}";
        return $"{FaceTuneConstants.GeneratedParameterPrefix}/Menu/{source.gameObject.name}_{Hash128.Compute(identity)}";
    }

    private static float GetIndividualDefaultValue(ResolvedMenuDefinition menu)
    {
        if (menu.Kind != MenuComponent.Kind.Toggle) return menu.DefaultValue;
        return (menu.DefaultValue != 0f) == (menu.SelectedValue != 0f) ? 1f : 0f;
    }

    private sealed record UnresolvedMenuDefinition(
        FaceTuneTagComponent Source,
        MenuComponent? ExistingMenu,
        MenuComponent.Kind Kind,
        bool UseExistingParameter,
        bool GenerateParameterGroup,
        string GroupName,
        string ParameterName,
        string Purpose,
        bool Synced,
        bool Saved,
        float DefaultValue,
        float SelectedValue)
    {
        public ResolvedMenuDefinition Resolve(string parameterName, float selectedValue)
            => new(
                Source,
                ExistingMenu,
                Kind,
                UseExistingParameter,
                GenerateParameterGroup,
                GroupName,
                parameterName,
                Synced,
                Saved,
                DefaultValue,
                selectedValue);
    }
}
