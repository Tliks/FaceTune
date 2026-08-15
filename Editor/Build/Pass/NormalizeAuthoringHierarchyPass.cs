using UnityEditor.Animations;

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
            context.PlatformSupport.GetAnimatorController(),
            out var menuParameters);
        context.SetAuthoringSettings(settings with { ParameterDomains = parameterDomains });
        context.SetMenuParameters(menuParameters);

        ResolveMenuConditions(root, context.PlatformSupport.GetAnimatorController());
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
            menu.Binding = MenuComponent.ParameterBinding.Generate;
            menu.Name = string.Empty;
            menu.GroupName = string.Empty;
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
            menu.Binding = expression.WriteMode == ExpressionWriteMode.Replace
                || !string.IsNullOrWhiteSpace(expression.DirectMenuSettings.GroupName)
                ? MenuComponent.ParameterBinding.GenerateGroup
                : MenuComponent.ParameterBinding.Generate;
            menu.Name = string.Empty;
            menu.GroupName = expression.WriteMode == ExpressionWriteMode.Replace
                ? BuiltInMenuGroups.DirectMenuReplace
                : expression.DirectMenuSettings.GroupName;

            var proxyObject = new GameObject($"{expression.name} (Direct Menu)");
            proxyObject.transform.SetParent(root.transform, false);
            var proxy = proxyObject.AddComponent<ExpressionComponent>();
            proxy.WriteMode = expression.WriteMode;
            proxy.AllowEyeBlink = expression.AllowEyeBlink;
            proxy.AllowLipSync = expression.AllowLipSync;
            proxy.MultiFrame = expression.MultiFrame;
            proxy.FacialBlendShapes.Direct = resolver.FacialData.Enumerate(expression)
                .SelectMany(item => item.Value.BlendShapeAnimations)
                .ToList() is var animations
                ? new FacialBlendShapeData { BlendShapeAnimations = animations }
                : new FacialBlendShapeData();
            proxy.HasEyeBlink = true;
            proxy.EyeBlink.Direct = resolver.EyeBlink.Get(expression);
            proxy.HasLipSync = true;
            proxy.LipSync.Direct = resolver.LipSync.Get(expression);
            proxy.HasTransition = true;
            proxy.Transition = resolver.Transition.Get(expression);
            proxy.HasPriority = true;
            proxy.Priority = resolver.Priority.Get(expression);
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
        AnimatorController? controller,
        out IReadOnlyList<MenuParameterPlan> menuParameters)
    {
        var menus = root.GetComponentsInChildren<MenuComponent>(true);
        var generatedNames = new HashSet<string>(StringComparer.Ordinal);
        var parameters = new List<MenuParameterPlan>();

        foreach (var menu in menus)
        {
            if (menu.MenuKind == MenuComponent.Kind.Folder) continue;
            if (menu.Binding == MenuComponent.ParameterBinding.GenerateGroup
                && string.IsNullOrWhiteSpace(menu.GroupName))
                menu.Binding = MenuComponent.ParameterBinding.Generate;
            if (menu.Binding == MenuComponent.ParameterBinding.GenerateGroup
                && menu.MenuKind != MenuComponent.Kind.Toggle)
                throw new InvalidOperationException($"Only toggle menus can use Generate Group: '{menu.name}'.");

            if (menu.Binding == MenuComponent.ParameterBinding.Generate)
            {
                if (string.IsNullOrWhiteSpace(menu.Name)) menu.Name = CreateAutomaticParameterName(root, menu);
                ValidateParameterName(menu.Name, menu);
                if (!generatedNames.Add(menu.Name))
                    throw new InvalidOperationException($"Generated menu parameter is duplicated: '{menu.Name}'.");
                parameters.Add(new MenuParameterPlan(
                    menu.Name,
                    menu.MenuKind == MenuComponent.Kind.Toggle ? MenuParameterType.Bool : MenuParameterType.Float,
                    ResolveGeneratedInitialValue(menu),
                    true));
            }
            else if (menu.Binding == MenuComponent.ParameterBinding.Existing)
            {
                ValidateParameterName(menu.Name, menu);
                ValidateExistingParameter(menu, controller);
            }
        }

        foreach (var group in menus
                     .Where(menu => menu.MenuKind == MenuComponent.Kind.Toggle
                                    && menu.Binding == MenuComponent.ParameterBinding.GenerateGroup)
                     .GroupBy(menu => menu.GroupName, StringComparer.Ordinal))
        {
            ValidateGroupName(group.Key, group.First());
            var parameterName = $"{FaceTuneConstants.GeneratedParameterPrefix}/MenuGroup/{group.Key}";
            if (!generatedNames.Add(parameterName))
                throw new InvalidOperationException($"Generated menu parameter is duplicated: '{parameterName}'.");

            var options = group.ToArray();
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
                true));
        }

        menuParameters = parameters;
        return parameterDomains;
    }

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

    private static void ValidateExistingParameter(MenuComponent menu, AnimatorController? controller)
    {
        var type = ResolveExistingParameterType(menu, controller);
        if (menu.MenuKind == MenuComponent.Kind.Radial && type != AnimatorControllerParameterType.Float
            || menu.MenuKind == MenuComponent.Kind.Toggle && type is not (AnimatorControllerParameterType.Bool or AnimatorControllerParameterType.Int))
            throw new InvalidOperationException($"Existing parameter '{menu.Name}' has an incompatible type for menu '{menu.name}'.");
    }

    private static AnimatorControllerParameterType ResolveExistingParameterType(MenuComponent menu, AnimatorController? controller)
    {
        var parameter = controller?.parameters.FirstOrDefault(item => item.name == menu.Name);
        if (parameter == null)
            throw new InvalidOperationException($"Existing menu parameter was not found: '{menu.Name}'.");
        return parameter.type;
    }

    // MenuConditionを正規化済みのparameter conditionへ変換する。
    private static void ResolveMenuConditions(GameObject root, AnimatorController? controller)
    {
        foreach (var source in root.GetComponentsInChildren<FaceTuneTagComponent>(true).OfType<IHasConditions>())
        foreach (var condition in source.Conditions)
        foreach (var conditionCase in condition.Cases)
        {
            for (var i = conditionCase.MenuConditions.Count - 1; i >= 0; i--)
            {
                var menuCondition = conditionCase.MenuConditions[i];
                if (menuCondition.MenuSource == null)
                    conditionCase.MenuConditions.RemoveAt(i);
                else
                    conditionCase.ParameterConditions.Add(ToParameterCondition(menuCondition, controller));
            }
            conditionCase.MenuConditions.Clear();
        }
    }

    private static void FilterExcludedBlendShapes(string bodyPath, GameObject root, AuthoringBuildSettings settings)
    {
        foreach (var component in root.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            switch (component)
            {
                case ExpressionComponent expression:
                    FilterFacialSource(expression.FacialBlendShapes, expression, bodyPath, settings.ExcludedBlendShapeNames);
                    if (expression.HasEyeBlink)
                        FilterEyeBlinkSource(expression.EyeBlink, expression, settings.ExcludedBlendShapeNames);
                    if (expression.HasLipSync)
                        FilterLipSyncSource(expression.LipSync, settings.ExcludedBlendShapeNames);
                    break;
                case SettingsComponent setting:
                    if (setting.HasFacialBlendShapes)
                        FilterFacialSource(setting.FacialBlendShapes, setting, bodyPath, settings.ExcludedBlendShapeNames);
                    if (setting.HasEyeBlink)
                        FilterEyeBlinkSource(setting.EyeBlink, setting, settings.ExcludedBlendShapeNames);
                    if (setting.HasLipSync)
                        FilterLipSyncSource(setting.LipSync, settings.ExcludedBlendShapeNames);
                    break;
                case ExpressionDataComponent data:
                    FilterFacialSource(data.FacialBlendShapes, data, bodyPath, settings.ExcludedBlendShapeNames);
                    break;
            }
        }
    }

    private static void FilterFacialSource(FacialBlendShapeDataSource source, Component owner, string bodyPath, IReadOnlyCollection<string> excluded)
    {
        if (source.SourceMode != SettingsSourceMode.Direct) return;
        if (source.Direct.Clip != null)
        {
            var animations = new List<BlendShapeWeightAnimation>();
            source.Direct.Clip.GetBlendShapeAnimations(source.Direct.ClipOption, animations, bodyPath);
            animations.AddRange(source.Direct.BlendShapeAnimations);
            source.Direct.BlendShapeAnimations = animations;
            source.Direct.Clip = null;
        }
        FilterAnimations(source.Direct.BlendShapeAnimations, owner, excluded);
    }

    private static void FilterEyeBlinkSource(EyeBlinkSettingsSource source, Component owner, IReadOnlyCollection<string> excluded)
    {
        if (source.SourceMode == SettingsSourceMode.Direct && source.Direct.EyeBlinkMode == EyeBlinkSettings.Kind.Automatic)
            FilterAnimations(source.Direct.Animations, owner, excluded);
    }

    private static void FilterLipSyncSource(LipSyncSettingsSource source, IReadOnlyCollection<string> excluded)
    {
        if (source.SourceMode != SettingsSourceMode.Direct) return;
        source.Direct.CancellerBlendShapes.RemoveAll(shape => excluded.Contains(shape.Name));
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

    private static ParameterCondition ToParameterCondition(MenuCondition condition, AnimatorController? controller)
    {
        var menu = condition.MenuSource!;
        if (menu.MenuKind == MenuComponent.Kind.Radial)
        {
            if (condition.Mode is not (MenuConditionMode.LessThan or MenuConditionMode.GreaterThan))
                throw new InvalidOperationException($"Radial menu '{menu.name}' requires a radial condition.");
            return ParameterCondition.Float(menu.Name, (ComparisonType)condition.Mode, condition.Threshold);
        }

        if (menu.MenuKind != MenuComponent.Kind.Toggle
            || condition.Mode is not (MenuConditionMode.Enabled or MenuConditionMode.Disabled))
            throw new InvalidOperationException($"Menu '{menu.name}' has an incompatible condition.");

        var isEnabled = condition.Mode == MenuConditionMode.Enabled;
        if (menu.Binding == MenuComponent.ParameterBinding.GenerateGroup)
            return ParameterCondition.Int(menu.Name, isEnabled ? ComparisonType.Equal : ComparisonType.NotEqual, (int)menu.SelectedValue);

        if (menu.Binding == MenuComponent.ParameterBinding.Generate)
            return ParameterCondition.Bool(menu.Name, isEnabled == (menu.SelectedValue != 0f));

        var type = ResolveExistingParameterType(menu, controller);
        return type == AnimatorControllerParameterType.Bool
            ? ParameterCondition.Bool(menu.Name, isEnabled == (menu.SelectedValue != 0f))
            : ParameterCondition.Int(menu.Name, isEnabled ? ComparisonType.Equal : ComparisonType.NotEqual, (int)menu.SelectedValue);
    }

}
