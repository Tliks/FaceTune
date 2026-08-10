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
        FilterBlendShapeOutputs(context.AvatarContext.BodyPath, root, settings);
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
            else if (source is ExpressionComponent ec && ec.Condition.IsEmpty)
            {
                ec.HasCondition = false;
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
                    menu.MenuKind == MenuComponent.Kind.Toggle && menu.InitialValue != 0f ? menu.SelectedValue : menu.InitialValue,
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
                     .GroupBy(menu => menu.Name, StringComparer.Ordinal))
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
            for (var i = conditionCase.Conditions.Count - 1; i >= 0; i--)
            {
                if (conditionCase.Conditions[i] is not MenuCondition menuCondition) continue;
                if (menuCondition.MenuSource == null)
                    conditionCase.Conditions.RemoveAt(i);
                else
                    conditionCase.Conditions[i] = ToParameterCondition(menuCondition, controller);
            }
        }
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
            return ParameterCondition.Bool(menu.Name, isEnabled);

        var type = ResolveExistingParameterType(menu, controller);
        return type == AnimatorControllerParameterType.Bool
            ? ParameterCondition.Bool(menu.Name, isEnabled == (menu.SelectedValue != 0f))
            : ParameterCondition.Int(menu.Name, isEnabled ? ComparisonType.Equal : ComparisonType.NotEqual, (int)menu.SelectedValue);
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
            FilterEyeBlinkSettings(component, settings);
        }

        foreach (var component in root.GetComponentsInChildren<LipSyncComponent>(true))
        {
            FilterAdvancedLipSyncSettings(component, settings);
        }
    }

    private static void FilterBlendShapeAnimations(FacialBlendShapeData data, Component owner, string bodyPath, AuthoringBuildSettings settings)
    {
        var animations = new List<BlendShapeWeightAnimation>();
        if (data.Clip != null)
            data.Clip.GetBlendShapeAnimations(data.ClipOption, animations, bodyPath);
        animations.AddRange(data.BlendShapeAnimations);

        data.BlendShapeAnimations = FilterBlendShapeAnimations(owner, animations, settings);
        data.Clip = null;
    }

    private static void FilterEyeBlinkSettings(EyeBlinkComponent component, AuthoringBuildSettings settings)
    {
        if (component.ReferenceMode != SettingsSourceMode.Direct) return;

        component.Settings = component.Settings with
        {
            Automatic = component.Settings.Automatic with
            {
                Animations = FilterBlendShapeAnimations(component, component.Settings.Automatic.Animations, settings)
            }
        };
    }

    private static void FilterAdvancedLipSyncSettings(LipSyncComponent component, AuthoringBuildSettings settings)
    {
        if (component.ReferenceMode != SettingsSourceMode.Direct) return;

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
