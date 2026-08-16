namespace Aoyon.FaceTune.Build;

/// <summary>
/// Normalizes authoring-only component forms before expression compilation.
/// The compiler should read normalized data/conditions instead of knowing every authoring shortcut.
/// </summary>
internal class NormalizeAuthoringHierarchyPass : FaceTunePass<NormalizeAuthoringHierarchyPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.normalize-authoring-hierarchy";
    public override string DisplayName => "Normalize Authoring Hierarchy";

    protected override void Execute(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        NormalizeExpressionSets(root);
        NormalizeDirectMenus(context);
        IgnoreEmptyCondition(root);

        var settings = context.RequireAuthoringSettings();
        var parameterDomains = NormalizeMenuParameters(
            root,
            settings.ParameterDomains,
            out var menuParameters);
        context.SetAuthoringSettings(settings with { ParameterDomains = parameterDomains });
        context.SetMenuParameters(menuParameters);

        ResolveMultiFrameMenus(root);
        ResolveMenuConditions(root);
        FilterExcludedBlendShapes(context.AvatarContext.BodyPath, root, settings);
    }


    private static void NormalizeExpressionSets(GameObject root)
    {
        foreach (var settings in root.GetComponentsInChildren<SettingsComponent>(true)
                     .Where(settings => settings.ExpressionSetEnabled))
        {
            var menu = settings.gameObject.EnsureComponent<MenuComponent>();
            menu.MenuKind = MenuComponent.Kind.Toggle;
            menu.Menu = settings.ExpressionSet.Menu;
            menu.UseExistingParameter = false;
            menu.GenerateParameterGroup = false;
            menu.Name = string.Empty;
            menu.GroupName = string.Empty;
            menu.Synced = true;
            menu.Saved = true;
            menu.InitialValue = settings.ExpressionSet.DefaultSelected ? 1f : 0f;
            menu.SelectedValue = 1f;

            settings.HasCondition = true;
            if (settings.Condition.Cases.Count == 0)
                settings.Condition.Cases.Add(new ConditionCase());
            foreach (var conditionCase in settings.Condition.Cases)
                conditionCase.MenuConditions.Add(MenuCondition.Enabled(menu));
        }
    }

    private static void NormalizeDirectMenus(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        var resolver = new FaceTuneResolver(root);
        foreach (var expression in root.GetComponentsInChildren<ExpressionComponent>(true)
                     .Where(expression => expression.DirectMenuEnabled))
        {
            var menu = expression.gameObject.EnsureComponent<MenuComponent>();
            menu.MenuKind = MenuComponent.Kind.Toggle;
            menu.Menu = expression.DirectMenuSettings.Menu;
            menu.UseExistingParameter = false;
            menu.GenerateParameterGroup = expression.WriteMode == ExpressionWriteMode.Replace
                || !string.IsNullOrWhiteSpace(expression.DirectMenuSettings.GroupName);
            menu.Name = string.Empty;
            menu.GroupName = expression.WriteMode == ExpressionWriteMode.Replace
                ? BuiltInMenuGroups.DirectMenuReplace
                : expression.DirectMenuSettings.GroupName;
            menu.Synced = true;
            menu.Saved = true;

            var proxyObject = new GameObject($"{expression.name} (Direct Menu)");
            proxyObject.transform.SetParent(root.transform, false);
            var proxy = proxyObject.AddComponent<ExpressionComponent>();
            proxy.WriteMode = expression.WriteMode;
            proxy.AllowEyeBlink = expression.AllowEyeBlink;
            proxy.AllowLipSync = expression.AllowLipSync;
            proxy.MultiFrame = expression.MultiFrame;
            proxy.FacialBlendShapes = resolver.FacialData.Enumerate(expression)
                .SelectMany(item => item.Value.BlendShapeAnimations)
                .ToList() is var animations
                ? new FacialBlendShapeData { BlendShapeAnimations = animations }
                : new FacialBlendShapeData();
            proxy.HasEyeBlink = true;
            proxy.EyeBlink = resolver.EyeBlink.Get(expression);
            proxy.HasLipSync = true;
            proxy.LipSync = resolver.LipSync.Get(expression);
            proxy.HasTransition = true;
            proxy.Transition = resolver.Transition.Get(expression);
            proxy.HasPriority = true;
            proxy.Priority = new PrioritySettings
            {
                Priority = resolver.Priority.Get(expression).Priority
                         + expression.DirectMenuSettings.PriorityOffset
            };
            proxy.HasCondition = true;
            proxy.Condition.Condition.Cases.Add(ConditionCase.From(MenuCondition.Enabled(menu)));
        }
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

            switch (source)
            {
                case ExpressionComponent expression
                    when expression.HasCondition
                      && expression.Condition.Mode == ConditionSelection.Kind.Conditional
                      && expression.Condition.Condition.IsEmpty:
                    expression.HasCondition = false;
                    break;
                case SettingsComponent settings when settings.HasCondition && settings.Condition.IsEmpty:
                    settings.HasCondition = false;
                    break;
                case AvatarControlComponent control
                    when control.Condition.Mode == ConditionSelection.Kind.Conditional
                      && control.Condition.Condition.IsEmpty:
                    control.Condition.Mode = ConditionSelection.Kind.Always;
                    break;
            }
        }
    }

    private static ParameterDomainRegistry NormalizeMenuParameters(
        GameObject root,
        ParameterDomainRegistry parameterDomains,
        out IReadOnlyList<MenuParameterPlan> menuParameters)
    {
        var menus = root.GetComponentsInChildren<MenuComponent>(true);
        var generatedNames = new HashSet<string>(StringComparer.Ordinal);
        var parameters = new List<MenuParameterPlan>();

        foreach (var menu in menus)
        {
            if (menu.MenuKind == MenuComponent.Kind.Folder) continue;
            if (menu.UseExistingParameter
                || menu.MenuKind != MenuComponent.Kind.Toggle
                || string.IsNullOrWhiteSpace(menu.GroupName))
                menu.GenerateParameterGroup = false;

            if (!menu.UseExistingParameter && !UsesGeneratedGroup(menu))
            {
                if (string.IsNullOrWhiteSpace(menu.Name)) menu.Name = CreateAutomaticParameterName(root, menu);
                ValidateParameterName(menu.Name, menu);
                if (!generatedNames.Add(menu.Name))
                    throw new InvalidOperationException($"Generated menu parameter is duplicated: '{menu.Name}'.");
                parameters.Add(new MenuParameterPlan(
                    menu.Name,
                    menu.MenuKind == MenuComponent.Kind.Toggle ? MenuParameterType.Bool : MenuParameterType.Float,
                    ResolveGeneratedInitialValue(menu),
                    menu.Synced,
                    menu.Saved));
            }
            else if (menu.UseExistingParameter)
            {
                ValidateParameterName(menu.Name, menu);
            }
        }

        foreach (var group in menus
                     .Where(UsesGeneratedGroup)
                     .GroupBy(menu => menu.GroupName, StringComparer.Ordinal))
        {
            ValidateGroupName(group.Key, group.First());
            var parameterName = $"{FaceTuneConstants.GeneratedParameterPrefix}/MenuGroup/{group.Key}";
            if (!generatedNames.Add(parameterName))
                throw new InvalidOperationException($"Generated menu parameter is duplicated: '{parameterName}'.");

            var options = group.ToArray();
            var parameterSettings = options[0];
            if (options.Any(menu => menu.Synced != parameterSettings.Synced
                                 || menu.Saved != parameterSettings.Saved))
                throw new InvalidOperationException($"Menu group '{group.Key}' has inconsistent sync or save settings.");
            var initial = options.Where(menu => menu.InitialValue != 0f).ToArray();
            if (initial.Length > 1)
                throw new InvalidOperationException($"Menu group '{group.Key}' has multiple initial options.");

            for (var index = 0; index < options.Length; index++)
            {
                options[index].Name = parameterName;
                options[index].SelectedValue = index + 1;
            }
            parameterDomains = parameterDomains.WithIntDomainOverride(
                parameterName,
                new IntParameterDomain(0, options.Length));
            parameters.Add(new MenuParameterPlan(
                parameterName,
                MenuParameterType.Int,
                initial.Length == 0 ? 0f : initial[0].SelectedValue,
                parameterSettings.Synced,
                parameterSettings.Saved));
        }

        menuParameters = parameters;
        return parameterDomains;
    }

    private static bool UsesGeneratedGroup(MenuComponent menu)
        => !menu.UseExistingParameter && menu.GenerateParameterGroup;

    private static float ResolveGeneratedInitialValue(MenuComponent menu)
    {
        if (menu.MenuKind != MenuComponent.Kind.Toggle) return menu.InitialValue;
        var selectedValue = menu.SelectedValue != 0f;
        var selectedByDefault = menu.InitialValue != 0f;
        return selectedByDefault == selectedValue ? 1f : 0f;
    }

    private static string CreateAutomaticParameterName(GameObject root, MenuComponent menu)
    {
        var indices = new Stack<int>();
        for (var current = menu.transform; current != root.transform; current = current.parent!)
            indices.Push(current.GetSiblingIndex());
        return $"{FaceTuneConstants.GeneratedParameterPrefix}/Menu/{menu.MenuKind}/{string.Join("/", indices)}";
    }

    private static void ValidateGroupName(string name, MenuComponent menu)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl))
            throw new InvalidOperationException($"Menu group name is invalid: '{menu.name}'.");
        ValidateParameterName($"{FaceTuneConstants.GeneratedParameterPrefix}/MenuGroup/{name}", menu);
    }

    private static void ValidateParameterName(string name, MenuComponent menu)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || name.Any(char.IsControl))
            throw new InvalidOperationException($"Menu parameter name is invalid: '{menu.name}'.");
    }

    private static void ResolveMultiFrameMenus(GameObject root)
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
            settings.ParameterName = ResolveMenuParameterName(settings.MenuSource);
            settings.MenuSource = null;
        }
    }

    private static void ResolveMenuConditions(GameObject root)
    {
        foreach (var source in root.GetComponentsInChildren<FaceTuneTagComponent>(true).OfType<IHasConditions>())
        foreach (var condition in source.Conditions)
        foreach (var conditionCase in condition.Cases)
        {
            foreach (var menuCondition in conditionCase.MenuConditions)
            {
                if (menuCondition.MenuSource != null)
                    conditionCase.ParameterConditions.Add(ResolveMenuCondition(menuCondition));
            }
            conditionCase.MenuConditions.Clear();
        }
    }

    private static ParameterCondition ResolveMenuCondition(MenuCondition condition)
    {
        var menu = condition.MenuSource!;
        var parameterName = ResolveMenuParameterName(menu);
        if (menu.MenuKind == MenuComponent.Kind.Radial)
        {
            if (condition.Mode is not (MenuConditionMode.LessThan or MenuConditionMode.GreaterThan))
                throw new InvalidOperationException($"Radial menu '{menu.name}' requires a radial condition.");
            return ParameterCondition.Float(
                parameterName,
                (ComparisonType)condition.Mode,
                condition.Threshold);
        }

        if (menu.MenuKind != MenuComponent.Kind.Toggle
            || condition.Mode is not (MenuConditionMode.Enabled or MenuConditionMode.Disabled))
            throw new InvalidOperationException($"Menu '{menu.name}' has an incompatible condition.");
        var isEnabled = condition.Mode == MenuConditionMode.Enabled;
        return !menu.UseExistingParameter && menu.GenerateParameterGroup
            ? ParameterCondition.Int(
                parameterName,
                isEnabled ? ComparisonType.Equal : ComparisonType.NotEqual,
                (int)menu.SelectedValue)
            : ParameterCondition.Bool(
                parameterName,
                isEnabled == (menu.SelectedValue != 0f));
    }

    private static void FilterExcludedBlendShapes(string bodyPath, GameObject root, AuthoringBuildSettings settings)
    {
        foreach (var component in root.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            switch (component)
            {
                case ExpressionComponent expression:
                    FilterFacialReference(expression.GetReferenceableSettings<FacialBlendShapeData>(), expression, bodyPath, settings.ExcludedBlendShapeNames);
                    if (expression.HasEyeBlink)
                        FilterEyeBlinkReference(expression.GetReferenceableSettings<EyeBlinkSettings>(), expression, settings.ExcludedBlendShapeNames);
                    if (expression.HasLipSync)
                        FilterLipSyncReference(expression.GetReferenceableSettings<LipSyncSettings>(), settings.ExcludedBlendShapeNames);
                    break;
                case SettingsComponent setting:
                    if (setting.HasFacialBlendShapes)
                        FilterFacialReference(setting.GetReferenceableSettings<FacialBlendShapeData>(), setting, bodyPath, settings.ExcludedBlendShapeNames);
                    if (setting.HasEyeBlink)
                        FilterEyeBlinkReference(setting.GetReferenceableSettings<EyeBlinkSettings>(), setting, settings.ExcludedBlendShapeNames);
                    if (setting.HasLipSync)
                        FilterLipSyncReference(setting.GetReferenceableSettings<LipSyncSettings>(), settings.ExcludedBlendShapeNames);
                    break;
                case ExpressionDataComponent data:
                    FilterFacialReference(data.GetReferenceableSettings<FacialBlendShapeData>(), data, bodyPath, settings.ExcludedBlendShapeNames);
                    break;
            }
        }
    }

    private static void FilterFacialReference(ReferenceableExpressionSettings<FacialBlendShapeData> settings, Component owner, string bodyPath, IReadOnlyCollection<string> excluded)
    {
        if (settings.Mode != SettingsReferenceMode.Direct) return;
        var value = settings.Direct;
        if (value.Clip != null)
        {
            var animations = new List<BlendShapeWeightAnimation>();
            value.Clip.GetBlendShapeAnimations(value.ClipOption, animations, bodyPath);
            animations.AddRange(value.BlendShapeAnimations);
            value.BlendShapeAnimations = animations;
            value.Clip = null;
        }
        FilterAnimations(value.BlendShapeAnimations, owner, excluded);
    }

    private static void FilterEyeBlinkReference(ReferenceableExpressionSettings<EyeBlinkSettings> settings, Component owner, IReadOnlyCollection<string> excluded)
    {
        if (settings.Mode != SettingsReferenceMode.Direct) return;
        switch (settings.Direct.EyeBlinkMode)
        {
            case EyeBlinkSettings.Kind.BuiltIn:
                return;
            case EyeBlinkSettings.Kind.SimpleAnimation:
                settings.Direct.SimpleBlinkBlendShapes.RemoveAll(shape => excluded.Contains(shape.Name));
                settings.Direct.SimpleConflictPreventionBlendShapes.RemoveAll(shape => excluded.Contains(shape.Name));
                return;
            case EyeBlinkSettings.Kind.CustomAnimation:
                FilterAnimations(settings.Direct.Animations, owner, excluded);
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void FilterLipSyncReference(ReferenceableExpressionSettings<LipSyncSettings> settings, IReadOnlyCollection<string> excluded)
    {
        if (settings.Mode != SettingsReferenceMode.Direct) return;
        settings.Direct.CancellerBlendShapes.RemoveAll(shape => excluded.Contains(shape.Name));
    }

    private static void FilterAnimations(List<BlendShapeWeightAnimation> animations, Component owner, IReadOnlyCollection<string> excluded)
    {
        List<string>? removed = null;
        for (var index = animations.Count - 1; index >= 0; index--)
        {
            var name = animations[index].Name;
            if (!excluded.Contains(name)) continue;
            animations.RemoveAt(index);
            if (removed == null || !removed.Contains(name)) (removed ??= new()).Add(name);
        }
        if (removed?.Count > 0)
            LocalizedLog.Warning("log.processTrackedShapesPass.unAllowedBlendShapesFound.warning", $"{owner}:{string.Join(", ", removed)}");
    }

    private static string ResolveMenuParameterName(MenuComponent menu)
        => menu.Name;

}
