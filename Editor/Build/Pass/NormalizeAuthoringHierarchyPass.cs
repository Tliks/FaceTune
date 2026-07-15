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
    private const string DirectMenuObjectName = FaceTuneConstants.Name + " DirectMenu";

    protected override void Execute(FaceTuneContext context)
    {
        ProcessPresetComponents(context.AvatarContext.Root);
        ProcessDirectMenuSettings(context.AvatarContext.Root);
        IgnoreEmptyCondition(context.AvatarContext.Root);
        AssignMenuParameters(context.AvatarContext.Root, context.RequireSettings().ParameterDomains);
        ResolveMenuConditions(context.AvatarContext.Root);
    }

    // Preset -> Menu + Conditionに変換
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
            var menu = preset.gameObject.EnsureComponent<MenuComponent>(); // Todo: 上書きしていいの？
            menu.Kind = MenuItemKind.Toggle;
            menu.MenuName = preset.MenuName;
            menu.Icon = preset.Icon;
            menu.InstallSettings = preset.InstallSettings;
            menu.DefaultSelected = preset == defaultPreset;
            menu.ExclusiveToggleGroup.GroupName = PresetParameterName;

            var condition = preset.gameObject.EnsureComponent<ConditionComponent>().Condition;
            if (condition.Always)
            {
                condition.Always = false;
                condition.Cases.Clear();
            }
            condition.Cases.Add(ConditionCase.From(MenuCondition.Enabled(menu)));
        }
    }

    // DirectMenu => Menu + 高優先度のExpression に変換
    private static void ProcessDirectMenuSettings(GameObject root)
    {
        var expressions = root.GetComponentsInChildren<FaceTuneComponent>(true)
            .Where(expression => expression.DirectMenuEnabled)
            .ToArray();
        
        if (expressions.Length == 0) return;

        var directMenuRoot = new GameObject(DirectMenuObjectName);
        directMenuRoot.transform.SetParent(root.transform);

        foreach (var original in expressions)
        {
            var settings = original.DirectMenuSettings;

            var proxyObject = new GameObject(original.name);
            proxyObject.transform.SetParent(directMenuRoot.transform);
            var proxy = proxyObject.AddComponent<FaceTuneComponent>();

            var menu = original.gameObject.AddComponent<MenuComponent>();
            menu.MenuName = settings.MenuName;
            menu.Icon = new MenuIconSettings
            {
                Mode = settings.Icon.Mode,
                ManualIcon = settings.Icon.ManualIcon,
                PreviewExpression = settings.Icon.PreviewExpression ?? proxy
            };
            menu.InstallSettings = settings.InstallSettings;
            menu.Kind = MenuItemKind.Toggle;
            menu.DefaultSelected = false;
            menu.ExclusiveToggleGroup.GroupName = GetDirectMenuExclusiveGroupName(original);

            proxy.ConditionEnabled = true;
            proxy.Condition = new Condition(ConditionCase.From(MenuCondition.Enabled(menu)));
            proxy.ExpressionSettings = original.ExpressionSettings;
            proxy.FacialSettings = original.FacialSettings;
            proxy.DataReference = new(original.gameObject);

            if (FacialStyleContext.TryGetFacialStyle(original.gameObject, out var originalStyle))
            {
                var proxyStyle = proxyObject.AddComponent<StyleComponent>();
                proxyStyle.DataReference = new(originalStyle.gameObject);
            }
        }
    }

    private static string GetDirectMenuExclusiveGroupName(FaceTuneComponent expression)
    {
        return expression.FacialSettings.WriteMode switch
        {
            ExpressionWriteMode.Replace => DirectReplaceGroupName,
            ExpressionWriteMode.Blend => expression.DirectMenuSettings.BlendExclusiveGroupName,
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
    private static void AssignMenuParameters(GameObject root, ParameterDomainRegistry parameterDomains)
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

        foreach (var (groupName, maxValue) in exclusiveGroupIndices)
        {
            var parameterName = exclusiveGroupParameterNames[groupName];
            parameterDomains.SetIntDomainOverride(parameterName, new IntParameterDomain(0, maxValue));
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
            for (var i = conditionCase.Conditions.Count - 1; i >= 0; i--)
            {
                if (conditionCase.Conditions[i] is not MenuCondition menuCondition) continue;
                if (menuCondition.MenuSource == null)
                    conditionCase.Conditions.RemoveAt(i);
                else
                    conditionCase.Conditions[i] = ToParameterCondition(menuCondition);
            }
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
