namespace Aoyon.FaceTune.Build;

/// <summary>
/// Normalizes authoring-only component forms before expression compilation.
/// The compiler should read normalized data/conditions instead of knowing every authoring shortcut.
/// </summary>
internal class NormalizeAuthoringHierarchyPass : FaceTunePass<NormalizeAuthoringHierarchyPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.normalize-authoring-hierarchy";
    public override string DisplayName => "Normalize Authoring Hierarchy";

    private const string PresetGroupName = "PresetIndex";
    private const string DirectMenuObjectName = FaceTuneConstants.Name + " DirectMenu";

    protected override void Execute(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        ProcessPresetComponents(root);
        ProcessDirectMenuSettings(root);
        NormalizeMenuInstallContainers(root);
        IgnoreEmptyCondition(root);

        var settings = context.RequireAuthoringSettings();
        settings = settings with { ParameterDomains = AssignMenuParameters(root, settings.ParameterDomains) };
        context.SetAuthoringSettings(settings);

        ResolveMenuConditions(root);
        FilterBlendShapeOutputs(context.AvatarContext.BodyPath, root, settings);
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
            menu.ExclusiveToggleGroup.GroupName = PresetGroupName;

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

            var proxyObject = new GameObject(original.name + " (Direct Menu)");
            proxyObject.transform.SetParent(directMenuRoot.transform);
            var proxy = proxyObject.AddComponent<FaceTuneComponent>();

            var menu = original.gameObject.AddComponent<MenuComponent>();
            menu.MenuName = settings.MenuName;
            menu.Icon = new MenuIconSettings
            {
                Mode = settings.Icon.Mode,
                ManualIcon = settings.Icon.ManualIcon,
                PreviewExpression = new AvatarObjectReference(
                    settings.Icon.PreviewExpression.Get(original) ?? proxy.gameObject)
            };
            menu.InstallSettings = settings.InstallSettings;
            menu.Kind = MenuItemKind.Toggle;
            menu.DefaultSelected = false;
            menu.ExclusiveToggleGroup.GroupName = GetDirectMenuExclusiveGroupName(original);

            proxy.ConditionEnabled = true;
            proxy.Condition = new Condition(ConditionCase.From(MenuCondition.Enabled(menu)));
            proxy.ExpressionSettings = original.ExpressionSettings;
            proxy.FacialSettings = original.FacialSettings;
            proxy.Data.DataReference = new(original.gameObject);

            if (FacialStyleContext.TryGetFacialStyle(original.gameObject, out var originalStyle))
            {
                var proxyStyle = proxyObject.AddComponent<StyleComponent>();
                proxyStyle.Data.DataReference = new(originalStyle.gameObject);
            }
        }
    }

    private static void NormalizeMenuInstallContainers(GameObject root)
    {
        var sources = root.GetComponentsInChildren<FaceTuneTagComponent>(true)
            .OfType<IHasMenuInstallSettings>()
            .ToArray();
        foreach (var source in sources)
        {
            var installSettings = source.InstallSettings;
            if (installSettings == null) continue;

            var component = (Component)source;
            var target = installSettings.InstallContainerOverride.Get(component);
            if (target == null || target.transform.IsChildOf(component.transform)) continue;
            component.transform.SetParent(target.transform, true);
        }
    }

    private static string GetDirectMenuExclusiveGroupName(FaceTuneComponent expression)
    {
        return expression.FacialSettings.WriteMode switch
        {
            ExpressionWriteMode.Replace => BuiltInMenuGroups.DirectMenuReplace,
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
    private static ParameterDomainRegistry AssignMenuParameters(GameObject root, ParameterDomainRegistry parameterDomains)
    {
        var exclusiveGroupParameterNames = new Dictionary<string, string>();
        var exclusiveGroupIndices = new Dictionary<string, int>();

        foreach (var menu in root.GetComponentsInChildren<MenuComponent>(true))
        {
            if (menu.Kind == MenuItemKind.Toggle && menu.ExclusiveToggleGroup.IsEnabled)
            {
                var groupName = menu.ExclusiveToggleGroup.GroupName;

                menu.ParameterName = exclusiveGroupParameterNames.GetOrAdd(
                    groupName,
                    CreateGeneratedParameterName("ExclusiveToggle", groupName));

                var index = exclusiveGroupIndices.TryGetValue(groupName, out var current) ? current + 1 : 1;
                exclusiveGroupIndices[groupName] = index;
                menu.ExclusiveToggleGroup.Value = index;
            }
            else if (string.IsNullOrWhiteSpace(menu.ParameterName))
            {
                var menuName = string.IsNullOrWhiteSpace(menu.MenuName) ? menu.name : menu.MenuName;
                menu.ParameterName = CreateGeneratedParameterName(
                    menu.Kind == MenuItemKind.Radial ? "Radial" : "Toggle",
                    menuName);
            }
        }

        foreach (var (groupName, maxValue) in exclusiveGroupIndices)
        {
            var parameterName = exclusiveGroupParameterNames[groupName];
            parameterDomains = parameterDomains.WithIntDomainOverride(
                parameterName,
                new IntParameterDomain(0, maxValue));
        }

        return parameterDomains;
    }

    private static string CreateGeneratedParameterName(string category, string baseName)
    {
        baseName = baseName
            .Replace(" ", "_")
            .Replace(".", "_")
            .Replace("/", "_");
        var guid = Guid.NewGuid().ToString("N")[..8];
        return $"{FaceTuneConstants.GeneratedParameterPrefix}/{category}/{baseName}_{guid}";
    }

    // MenuContionをParamterConditionに変換
    private static void ResolveMenuConditions(GameObject root)
    {
        var sources = root.GetComponentsInChildren<FaceTuneTagComponent>(true);
        foreach (var source in sources.OfType<IHasConditions>())
        {
            foreach (var condition in source.Conditions)
                ResolveMenuConditions(condition);
        }

        foreach (var source in sources.OfType<IHasSingleConditions>())
        {
            foreach (var condition in source.SingleConditions)
            {
                if (condition.Condition is not MenuCondition menuCondition) continue;
                condition.Condition = menuCondition.MenuSource == null
                    ? null
                    : ToParameterCondition(menuCondition);
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

    private static void FilterBlendShapeOutputs(string bodyPath, GameObject root, AuthoringBuildSettings settings)
    {
        foreach (var component in root.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            if (component is not IHasExpressionData) continue;
            foreach (var data in component.EnumerateDataGraph())
            {
                FilterBlendShapeAnimations(data, component, bodyPath, settings);
            }
        }

        foreach (var component in root.GetComponentsInChildren<EyeBlinkComponent>(true))
        {
            FilterAdvancedEyeBlinkSettings(component, settings);
        }

        foreach (var component in root.GetComponentsInChildren<LipSyncComponent>(true))
        {
            FilterAdvancedLipSyncSettings(component, settings);
        }
    }

    private static void FilterBlendShapeAnimations(ExpressionData data, Component owner, string bodyPath, AuthoringBuildSettings settings)
    {
        var animations = new List<BlendShapeWeightAnimation>();
        if (data.Clip != null)
            data.Clip.GetBlendShapeAnimations(data.ClipOption, animations, bodyPath);
        animations.AddRange(data.BlendShapeAnimations);

        data.BlendShapeAnimations = FilterBlendShapeAnimations(owner, animations, settings);
        data.Clip = null;
    }

    private static void FilterAdvancedEyeBlinkSettings(EyeBlinkComponent component, AuthoringBuildSettings settings)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Direct) return;

        var advancedSettings = component.AdvancedEyeBlinkSettings;
        component.AdvancedEyeBlinkSettings = advancedSettings with
        {
            BlinkBlendShapeNames = FilterBlendShapeNames(component, advancedSettings.BlinkBlendShapeNames, settings),
            CancelerBlendShapeNames = FilterBlendShapeNames(component, advancedSettings.CancelerBlendShapeNames, settings)
        };
    }

    private static void FilterAdvancedLipSyncSettings(LipSyncComponent component, AuthoringBuildSettings settings)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Direct) return;

        var advancedSettings = component.AdvancedLipSyncSettings;
        component.AdvancedLipSyncSettings = advancedSettings with
        {
            CancelerBlendShapeNames = FilterBlendShapeNames(component, advancedSettings.CancelerBlendShapeNames, settings)
        };
    }

    private static List<BlendShapeWeightAnimation> FilterBlendShapeAnimations(
        Component owner,
        IEnumerable<BlendShapeWeightAnimation> animations,
        AuthoringBuildSettings settings)
    {
        var list = animations.ToList();
        WarnExcludedBlendShapes(owner, list.Select(animation => animation.Name), settings.ExcludedBlendShapeNames);
        return list
            .Where(animation => !settings.ExcludedBlendShapeNames.Contains(animation.Name))
            .ToList();
    }

    private static List<string> FilterBlendShapeNames(Component owner, IEnumerable<string> names, AuthoringBuildSettings settings)
    {
        var list = names.ToList();
        WarnExcludedBlendShapes(owner, list, settings.ExcludedBlendShapeNames);
        return list
            .Where(name => !settings.ExcludedBlendShapeNames.Contains(name))
            .ToList();
    }

    private static void WarnExcludedBlendShapes(Component owner, IEnumerable<string> names, IReadOnlyCollection<string> excludedBlendShapeNames)
    {
        var removed = names
            .Where(excludedBlendShapeNames.Contains)
            .Distinct()
            .ToList();

        if (removed.Count == 0) return;

        LocalizedLog.Warning(
            "log.processTrackedShapesPass.unAllowedBlendShapesFound.warning",
            $"{owner}:{string.Join(", ", removed)}");
    }
}
