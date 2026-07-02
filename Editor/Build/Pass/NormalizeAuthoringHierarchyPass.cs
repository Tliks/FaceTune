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
        var settings = context.BuildContext.GetState(_ => FaceTuneBuildSettings.Default);

        NormalizePresetComponents(context.AvatarContext.Root);
        NormalizeMenuComponents(context.AvatarContext.Root);
        NormalizeMenuConditions(context.AvatarContext.Root);
        NormalizeExpressionData(context.AvatarContext.BodyPath, context.AvatarContext.Root, settings);
    }

    // Preset -> Menu + Conditionに変更
    private static void NormalizePresetComponents(GameObject root)
    {
        var presets = root.GetComponentsInChildren<PresetComponent>(true);

        var defaultSelectedPresets = presets.Where(preset => preset.DefaultSelected).ToArray();
        if (defaultSelectedPresets.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:NormalizeAuthoringHierarchyPass:MultipleDefaultSelectedPreset", null, defaultSelectedPresets);
        }
        var defaultIndex = defaultSelectedPresets.Length == 1
            ? Array.IndexOf(presets, defaultSelectedPresets[0])
            : 0;

        for (var index = 0; index < presets.Length; index++)
        {
            var preset = presets[index];

            var menu = preset.gameObject.EnsureComponent<MenuComponent>();
            menu.Kind = MenuItemKind.Toggle;
            menu.Icon = preset.Icon;
            menu.InstallSettings = preset.InstallSettings;
            menu.ParameterName = PresetParameterName;
            menu.ExclusiveToggleGroup.GroupName = PresetParameterName;
            menu.ExclusiveToggleGroup.DefaultSelected = index == defaultIndex;

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
    private static void NormalizeMenuComponents(GameObject root)
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
    private static void NormalizeMenuConditions(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<ConditionComponent>(true))
        {
            NormalizeMenuConditions(component.Condition);
        }

        foreach (var component in root.GetComponentsInChildren<FaceTuneComponent>(true))
        {
            NormalizeMenuConditions(component.Condition);
        }
    }

    private static void NormalizeMenuConditions(Condition condition)
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

    // ExpressionDataの参照/Clipを解決&非許容ブレンドシェイプを削除
    private static void NormalizeExpressionData(string bodyPath, GameObject root, FaceTuneBuildSettings settings)
    {
        foreach (var component in root.GetComponentsInChildren<FaceTuneComponent>(true))
        {
            NormalizeData(component.Data, component, bodyPath, settings);
        }

        foreach (var component in root.GetComponentsInChildren<DataComponent>(true))
        {
            NormalizeData(component.Data, component, bodyPath, settings);
        }

        foreach (var component in root.GetComponentsInChildren<StyleComponent>(true))
        {
            NormalizeData(component.Data, component, string.Empty, settings);
        }
    }

    private static void NormalizeData(ExpressionData data, Component owner, string bodyPath, FaceTuneBuildSettings settings)
    {
        var animations = new List<BlendShapeWeightAnimation>();
        ExpressionDataUtility.AddAnimations(data, animations, bodyPath);

        var removed = animations
            .Where(animation => settings.ExcludedBlendShapeNames.Contains(animation.Name))
            .Select(animation => animation.Name)
            .Distinct()
            .ToList();

        if (removed.Count != 0)
        {
            LocalizedLog.Warning(
                "Log:warning:ProcessTrackedShapesPass:UnAllowedBlendShapesFound",
                $"{owner}:{string.Join(", ", removed)}");
        }

        data.BlendShapeAnimations = animations
            .Where(animation => !settings.ExcludedBlendShapeNames.Contains(animation.Name))
            .ToList();
        data.Clip = null;
    }
}