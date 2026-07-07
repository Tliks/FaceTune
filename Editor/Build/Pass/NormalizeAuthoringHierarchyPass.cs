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
    private const string DirectReplaceGroupName = $"{FaceTuneConstants.ParameterPrefix}/DirectMenu/Replace";
    private const string DirectMenuObjectName = "DirectMenu";

    protected override void Execute(FaceTuneContext context)
    {
        ProcessPresetComponents(context.AvatarContext.Root);
        ProcessDirectMenuSettings(context.AvatarContext.Root);
        IgnoreEmptyCondition(context.AvatarContext.Root);
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
            menu.DefaultSelected = preset == defaultPreset;
            menu.ExclusiveToggleGroup.GroupName = PresetParameterName;

            var condition = preset.gameObject.AddComponent<ConditionComponent>();
            condition.Condition = new Condition(ConditionCase.From(MenuCondition.Enabled(menu)));
        }
    }

    private static void ProcessDirectMenuSettings(GameObject root)
    {
        var expressions = root.GetComponentsInChildren<FaceTuneComponent>(true);

        var directMenuByExpression = new Dictionary<FaceTuneComponent, MenuComponent>();
        var directMenusByGroup = new Dictionary<string, List<MenuComponent>>();

        foreach (var expression in expressions)
        {
            if (!expression.DirectMenuEnabled) continue;

            var menu = CreateDirectMenu(expression);
            var groupName = menu.ExclusiveToggleGroup.GroupName;

            directMenuByExpression[expression] = menu;

            if (!string.IsNullOrWhiteSpace(groupName))
            {
                directMenusByGroup.GetOrAdd(groupName, new List<MenuComponent>()).Add(menu);
            }
        }

        foreach (var (expression, menu) in directMenuByExpression)
        {
            var groupName = menu.ExclusiveToggleGroup.GroupName;
            var gate = !string.IsNullOrWhiteSpace(groupName)
                       && directMenusByGroup.TryGetValue(groupName, out var groupMenus)
                ? new Condition(ConditionCase.From(groupMenus.Select(MenuCondition.Disabled).ToArray()))
                : null;

            var modifier = expression.gameObject.AddComponent<ExpressionConditionModifierComponent>();
            if (gate != null) modifier.OriginalGate = gate;
            modifier.AdditionalActivation = new Condition(ConditionCase.From(MenuCondition.Enabled(menu)));
        }
    }

    private static MenuComponent CreateDirectMenu(FaceTuneComponent expression)
    {
        var settings = expression.DirectMenuSettings;

        // 自身に配置するとMultipleComponentエラーになる可能性があるので、生成先は子にする
        var itemObject = new GameObject(DirectMenuObjectName);
        itemObject.transform.SetParent(expression.transform, false);

        var menu = itemObject.AddComponent<MenuComponent>();

        menu.MenuName = settings.MenuName;
        menu.Icon = settings.Icon;
        menu.InstallSettings = settings.InstallSettings;

        menu.Kind = MenuItemKind.Toggle;
        menu.DefaultSelected = false;
        menu.ExclusiveToggleGroup.GroupName = GetDirectMenuSuppressionGroup(expression);

        return menu;
    }

    private static string GetDirectMenuSuppressionGroup(FaceTuneComponent expression)
    {
        var settings = expression.DirectMenuSettings;
        return settings.SupressMode switch
        {
            DirectMenuSuppressionMode.Auto when expression.FacialSettings.WriteMode == ExpressionWriteMode.Replace => DirectReplaceGroupName,
            DirectMenuSuppressionMode.Auto when expression.FacialSettings.WriteMode == ExpressionWriteMode.Blend => settings.GroupName,
            DirectMenuSuppressionMode.Replace => DirectReplaceGroupName,
            DirectMenuSuppressionMode.Group => settings.GroupName,
            _ => string.Empty
        };
    }

    private static void IgnoreEmptyCondition(GameObject root)
    {
        foreach (var source in root.GetComponentsInChildren<FaceTuneTagComponent>(true)
            .OfType<IHasConditions>())
        {
            foreach (var condition in source.Conditions)
            {
                condition.Cases.RemoveAll(conditionCase => conditionCase.IsEmpty);
            }

            if (source is ConditionComponent cc && cc.Condition.IsEmpty)
            {
                Object.DestroyImmediate(cc);
            }
            else if (source is FaceTuneComponent ftc && ftc.Condition.IsEmpty)
            {
                ftc.ConditionEnabled = false;
            }
        }
    }

    // パラメータ名を確定、排他グループはValueも割り振り
    private static void AssignMenuParameters(GameObject root)
    {
        var exclusiveGroupParameterNames = new Dictionary<string, string>();
        var exclusiveGroupIndices = new Dictionary<string, int>();

        foreach (var menu in root.GetComponentsInChildren<MenuComponent>(true))
        {
            if (menu.Kind == MenuItemKind.Toggle && menu.ExclusiveToggleGroup.IsEnabled)
            {
                var groupName = menu.ExclusiveToggleGroup.GroupName;

                menu.ParameterName = exclusiveGroupParameterNames.GetOrAdd(groupName, CreateUniqueParameterName(groupName, "exclusive"));

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

    private static string CreateUniqueParameterName(string baseName, string suffix)
    {
        baseName = baseName.Replace(" ", "_").Replace(".", "_");
        var guid = Guid.NewGuid().ToString("N")[..8];
        return $"{FaceTuneConstants.ParameterPrefix}/{baseName}_{suffix}_{guid}";
    }

    // MenuContionをParamterConditionに変換
    private static void ResolveMenuConditions(GameObject root)
    {
        foreach (var source in root.GetComponentsInChildren<FaceTuneTagComponent>(true).OfType<IHasConditions>())
        {
            foreach (var condition in source.Conditions)
            {
                ResolveMenuConditions(condition);
            }
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
