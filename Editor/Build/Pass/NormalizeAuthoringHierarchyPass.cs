using nadena.dev.modular_avatar.core;

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
    private const string AutoMenuGroupName = "AutoMenu";

    protected override void Execute(FaceTuneContext context)
    {
        ProcessPresetComponents(context.AvatarContext.Root);
        ProcessAutoMenuComponents(context.AvatarContext.Root);
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

    // auto menu => Menu + Internal Condition(Modifier)に変更
    private static void ProcessAutoMenuComponents(GameObject root)
    {
        var autoMenus = root.GetComponentsInChildren<AutoMenuGeneratorComponent>(true);
        if (autoMenus.Length == 0) return;

        if (autoMenus.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:AutoMenuPlan:MultipleAutoMenu", null, autoMenus);
        }
        var autoMenu = autoMenus[0];

        var expressions = root.GetComponentsInChildren<FaceTuneComponent>(true);
        if (expressions.Length == 0) return;

        var excludedFromMenu = ResolveReferencedExpressions(autoMenu, autoMenu.ExcludeFromMenuTargets);
        var allowedDuringManualLock = ResolveReferencedExpressions(autoMenu, autoMenu.AllowDuringManualLockTargets);
        var suppressedExpressions = expressions
            .Where(expression => !allowedDuringManualLock.Contains(expression))
            .ToHashSet();

        var noneMenu = CreateAutoMenuItem(autoMenu.transform, "None", defaultSelected: true);
        var expressionMenus = CreateAutoMenuExpressionItems(
            autoMenu.transform,
            expressions.Where(expression => !excludedFromMenu.Contains(expression)).ToArray());
        AddAutoMenuConditionModifiers(expressions, suppressedExpressions, noneMenu, expressionMenus);
    }

    private static HashSet<FaceTuneComponent> ResolveReferencedExpressions(
        AutoMenuGeneratorComponent autoMenu,
        IEnumerable<AvatarObjectReference> references)
    {
        return references
            .Select(reference => reference.Get(autoMenu))
            .Where(target => target != null)
            .SelectMany(target => target.GetComponentsInChildren<FaceTuneComponent>(true))
            .ToHashSet();
    }

    private static MenuComponent CreateAutoMenuItem(Transform parent, string menuName, bool defaultSelected)
    {
        var itemObject = new GameObject(menuName);
        itemObject.transform.SetParent(parent, false);

        var menu = itemObject.AddComponent<MenuComponent>();
        menu.MenuName = menuName;
        menu.Kind = MenuItemKind.Toggle;
        menu.DefaultSelected = defaultSelected;
        menu.ExclusiveToggleGroup.GroupName = AutoMenuGroupName;
        return menu;
    }

    private static Dictionary<FaceTuneComponent, MenuComponent> CreateAutoMenuExpressionItems(
        Transform parent,
        IReadOnlyList<FaceTuneComponent> expressions)
    {
        var result = new Dictionary<FaceTuneComponent, MenuComponent>();
        var commonRoot = FindCommonParent(expressions);
        var folderCopies = new Dictionary<Transform, Transform>();

        foreach (var expression in expressions)
        {
            var menuParent = GetOrCreateFolderCopy(expression.transform.parent, parent, commonRoot, folderCopies);
            result.Add(expression, CreateAutoMenuItem(menuParent, expression.name, defaultSelected: false));
        }

        return result;
    }

    private static Transform? FindCommonParent(IReadOnlyList<FaceTuneComponent> expressions)
    {
        var parents = expressions
            .Select(expression => expression.transform.parent)
            .Where(parent => parent != null)
            .ToArray();
        if (parents.Length == 0) return null;

        var commonAncestors = GetAncestorsAndSelf(parents[0]).ToHashSet();
        foreach (var parent in parents.Skip(1))
        {
            commonAncestors.IntersectWith(GetAncestorsAndSelf(parent));
        }

        return GetAncestorsAndSelf(parents[0]).First(commonAncestors.Contains);

        static IEnumerable<Transform> GetAncestorsAndSelf(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                yield return current;
                current = current.parent;
            }
        }
    }

    private static Transform GetOrCreateFolderCopy(
        Transform? source,
        Transform generatedRoot,
        Transform? commonRoot,
        Dictionary<Transform, Transform> folderCopies)
    {
        if (source == null || source == commonRoot) return generatedRoot;
        if (folderCopies.TryGetValue(source, out var copy)) return copy;

        var parent = GetOrCreateFolderCopy(source.parent, generatedRoot, commonRoot, folderCopies);
        var obj = new GameObject(source.name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MenuFolderComponent>().MenuName = source.name;

        folderCopies.Add(source, obj.transform);
        return obj.transform;
    }

    private static void AddAutoMenuConditionModifiers(
        IEnumerable<FaceTuneComponent> expressions,
        HashSet<FaceTuneComponent> suppressedExpressions,
        MenuComponent noneMenu,
        IReadOnlyDictionary<FaceTuneComponent, MenuComponent> expressionMenus)
    {
        foreach (var expression in expressions)
        {
            var hasOriginalGate = suppressedExpressions.Contains(expression);
            var hasAdditionalActivation = expressionMenus.TryGetValue(expression, out var additionalActivation);
            if (!hasOriginalGate && !hasAdditionalActivation) continue;

            var modifier = expression.gameObject.AddComponent<ExpressionConditionModifierComponent>();
            if (hasOriginalGate) modifier.OriginalGate = MenuEnabledCondition(noneMenu);
            if (hasAdditionalActivation) modifier.AdditionalActivation = MenuEnabledCondition(additionalActivation);
        }
    }

    private static Condition MenuEnabledCondition(MenuComponent menu)
    {
        return new Condition
        {
            Cases =
            {
                new ConditionCase
                {
                    MenuConditions =
                    {
                        new MenuCondition
                        {
                            MenuSource = menu,
                            Mode = MenuConditionMode.Enabled
                        }
                    }
                }
            }
        };
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