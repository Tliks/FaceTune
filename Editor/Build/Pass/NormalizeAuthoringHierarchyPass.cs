namespace Aoyon.FaceTune.Build;

/// <summary>
/// Normalizes authoring-only component forms before expression compilation.
/// The compiler should read normalized data/conditions instead of knowing every authoring shortcut.
/// </summary>
internal class NormalizeAuthoringHierarchyPass : FaceTunePass<NormalizeAuthoringHierarchyPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.normalize-authoring-hierarchy";
    public override string DisplayName => "Normalize Authoring Hierarchy";

    private const string PresetParameterName = $"{FaceTuneConstants.ParameterPrefix}/PresetIndex";

    protected override void Execute(FaceTuneContext context)
    {
        ProcessPresetComponents(context.AvatarContext.Root);
        AssignMenuParameters(context.AvatarContext.Root);
        ResolveMenuConditions(context.AvatarContext.Root);
    }

    // Preset -> Menu + Conditionに変更
    private static void ProcessPresetComponents(GameObject root)
    {
        var presets = root.GetComponentsInChildren<PresetComponent>(true);

        var defaultSelectedPresets = presets.Where(preset => preset.DefaultSelected).ToArray();
        if (defaultSelectedPresets.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:NormalizeAuthoringHierarchyPass:MultipleDefaultSelectedPreset", null, defaultSelectedPresets);
        }
        var defaultPreset = defaultSelectedPresets.FirstOrDefault();

        foreach (var preset in presets)
        {
            var menu = preset.gameObject.EnsureComponent<MenuComponent>();
            menu.Kind = MenuItemKind.Toggle;
            menu.Icon = preset.Icon;
            menu.InstallSettings = preset.InstallSettings;
            menu.ParameterName = PresetParameterName;
            menu.ExclusiveToggleGroup.GroupName = PresetParameterName;
            menu.ExclusiveToggleGroup.DefaultSelected = preset == defaultPreset;

            var condition = preset.gameObject.AddComponent<ConditionComponent>();
            condition.Condition.Always = false;
            condition.Condition.Cases.Add(new ConditionCase
            {
                MenuConditions =
                {
                    new MenuCondition
                    {
                        MenuSource = menu,
                        Mode = MenuConditionMode.Enabled
                    }
                }
            });
        }
    }

    // パラメータ名を確定、排他グループはValueも割り振り
    private static void AssignMenuParameters(GameObject root)
    {
        var exclusiveGroupParameterNames = new Dictionary<string, string>();
        var exclusiveGroupIndices = new Dictionary<string, int>();

        foreach (var menu in root.GetComponentsInChildren<MenuComponent>(true))
        {
            if (menu.ExclusiveToggleGroup.IsEnabled)
            {
                var groupName = menu.ExclusiveToggleGroup.GroupName;

                menu.ParameterName = exclusiveGroupParameterNames.GetOrAdd(groupName, CreateGroupParameterName);

                var index = exclusiveGroupIndices.TryGetValue(groupName, out var current) ? current + 1 : 1;
                exclusiveGroupIndices[groupName] = index;
                menu.ExclusiveToggleGroup.Value = index;
            }
            else if (string.IsNullOrWhiteSpace(menu.ParameterName))
            {
                menu.ParameterName = CreateUniqueParameterName(menu.name, menu.Kind == MenuItemKind.Radial ? "radial" : "toggle");
            }
        }
    }

    private static string CreateGroupParameterName(string groupName)
    {
        if (groupName.StartsWith($"{FaceTuneConstants.ParameterPrefix}/")) return groupName;
        var safeName = SanitizeName(groupName);
        return $"{FaceTuneConstants.ParameterPrefix}/{safeName}/exclusive";
    }

    private static string CreateUniqueParameterName(string baseName, string suffix)
    {
        baseName = SanitizeName(baseName);
        var guid = Guid.NewGuid().ToString("N")[..8];
        return $"{FaceTuneConstants.ParameterPrefix}/{baseName}_{suffix}_{guid}";
    }

    private static string SanitizeName(string name)
    {
        return name.Replace(" ", "_").Replace(".", "_");
    }

    // MenuContionをParamterConditionに変換
    private static void ResolveMenuConditions(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<ConditionComponent>(true))
        {
            ResolveMenuConditions(component.Condition);
        }

        foreach (var component in root.GetComponentsInChildren<FaceTuneComponent>(true))
        {
            ResolveMenuConditions(component.Condition);
        }
    }

    private static void ResolveMenuConditions(Condition condition)
    {
        foreach (var conditionCase in condition.Cases)
        {
            foreach (var menuCondition in conditionCase.MenuConditions)
            {
                if (menuCondition.MenuSource == null) continue;
                conditionCase.ParameterConditions.Add(ToParameterCondition(menuCondition));
            }

            conditionCase.MenuConditions.Clear();
        }
    }

    private static ParameterCondition ToParameterCondition(MenuCondition condition)
    {
        var menu = condition.MenuSource!;

        switch (menu.Kind)
        {
            case MenuItemKind.Radial:
                return condition.Mode switch
                {
                    MenuConditionMode.LessThan => ParameterCondition.Float(
                        menu.ParameterName, ComparisonType.LessThan, condition.Threshold),
                    MenuConditionMode.GreaterThan => ParameterCondition.Float(
                        menu.ParameterName, ComparisonType.GreaterThan, condition.Threshold),
                    _ => throw new InvalidOperationException(
                        $"Radial menu '{menu.name}' has invalid condition mode '{condition.Mode}'. Use LessThan or GreaterThan.")
                };

            case MenuItemKind.Toggle when menu.ExclusiveToggleGroup.IsEnabled:
                return condition.Mode switch
                {
                    MenuConditionMode.Enabled => ParameterCondition.Int(
                        menu.ParameterName, ComparisonType.Equal, menu.ExclusiveToggleGroup.Value),
                    MenuConditionMode.Disabled => ParameterCondition.Int(
                        menu.ParameterName, ComparisonType.NotEqual, menu.ExclusiveToggleGroup.Value),
                    _ => throw new InvalidOperationException(
                        $"Exclusive toggle '{menu.name}' has invalid condition mode '{condition.Mode}'. Use Enabled or Disabled.")
                };

            case MenuItemKind.Toggle:
                return condition.Mode switch
                {
                    MenuConditionMode.Enabled => ParameterCondition.Bool(menu.ParameterName, true),
                    MenuConditionMode.Disabled => ParameterCondition.Bool(menu.ParameterName, false),
                    _ => throw new InvalidOperationException(
                        $"Toggle '{menu.name}' has invalid condition mode '{condition.Mode}'. Use Enabled or Disabled.")
                };

            default:
                throw new InvalidOperationException($"Unknown menu kind: {menu.Kind}");
        }
    }

}