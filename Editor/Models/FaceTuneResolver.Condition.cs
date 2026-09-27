using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune;

internal sealed class ConditionResolver
{
    private readonly GameObject _root;
    private readonly IMetaversePlatformSupport _platformSupport;
    private readonly ParameterDomainRegistry _parameterDomains;

    public ConditionResolver(
        GameObject root,
        IMetaversePlatformSupport platformSupport,
        ParameterDomainRegistry parameterDomains)
    {
        _root = root;
        _platformSupport = platformSupport;
        _parameterDomains = parameterDomains;
    }

    public DnfCondition Resolve(ExpressionComponent expression)
    {
        if (!expression.HasCondition) return DnfCondition.Never;

        var conditions = _root.GetComponentsInParentExcludingSelf<SettingsComponent>(expression, true)
            .Where(settings => settings.HasCondition)
            .Select(settings => settings.Condition);
        if (expression.Condition.Mode == ConditionSelection.Kind.Conditional)
            conditions = conditions.Append(expression.Condition.Condition);

        return DnfCondition.All(conditions.Select(Resolve));
    }

    public DnfCondition Resolve(ConditionSelection selection)
        => selection.Mode == ConditionSelection.Kind.Always
            ? DnfCondition.Always
            : Resolve(selection.Condition);

    public DnfCondition Resolve(Condition condition)
        => DnfCondition.Any(condition.Cases
            .Select(ResolveConditionCase)
            .OfType<DnfCondition>());

    public DnfCondition Resolve(MenuCondition condition)
        => ResolveRule(condition) ?? DnfCondition.Never;

    private DnfCondition? ResolveConditionCase(ConditionCase conditionCase)
    {
        var resolved = conditionCase.HandGestureConditions.Select(ResolveRule)
            .Concat(conditionCase.MenuConditions.Select(ResolveRule))
            .Concat(conditionCase.ParameterConditions.Select(ResolveRule))
            .OfType<DnfCondition>()
            .ToArray();
        return resolved.Length == 0 ? null : DnfCondition.All(resolved);
    }

    private DnfCondition? ResolveRule(HandGestureCondition condition)
        => _platformSupport.ResolveHandGestureCondition(condition, _parameterDomains);

    private DnfCondition? ResolveRule(ParameterCondition condition)
        => _platformSupport.ResolveParameterCondition(condition, _parameterDomains);

    private DnfCondition? ResolveRule(MenuCondition condition)
    {
        // 参照が切れたMenuConditionは条件自体を無かったこととして扱う。
        return condition.MenuSource == null
            ? null
            : _platformSupport.ResolveParameterCondition(
                ToParameterCondition(condition), _parameterDomains);
    }

    private static ParameterCondition ToParameterCondition(MenuCondition condition)
    {
        var menu = condition.MenuSource!;
        if (menu.MenuKind == MenuComponent.Kind.Radial)
        {
            if (condition.Mode is not (MenuConditionMode.LessThan or MenuConditionMode.GreaterThan))
            {
                throw new InvalidOperationException(
                    $"Radial menu '{menu.name}' requires a radial condition.");
            }
            var comparison = condition.Mode switch
            {
                MenuConditionMode.GreaterThan => ComparisonType.GreaterThan,
                MenuConditionMode.LessThan => ComparisonType.LessThan,
                _ => throw new ArgumentOutOfRangeException()
            };
            return ParameterCondition.Float(
                menu.ParameterName,
                comparison,
                condition.Threshold);
        }

        if (menu.MenuKind != MenuComponent.Kind.Toggle
            || condition.Mode is not (MenuConditionMode.Enabled or MenuConditionMode.Disabled))
        {
            throw new InvalidOperationException(
                $"Menu '{menu.name}' has an incompatible condition.");
        }
        var isEnabled = condition.Mode == MenuConditionMode.Enabled;
        var usesGeneratedGroup = !menu.UseExistingParameter && menu.GenerateParameterGroup;
        var usesExistingInt = menu.UseExistingParameter
                              && menu.ExistingToggleParameterType == MenuComponent.ToggleParameterType.Int;
        if (usesGeneratedGroup || usesExistingInt)
        {
            return ParameterCondition.Int(
                menu.ParameterName,
                isEnabled ? ComparisonType.Equal : ComparisonType.NotEqual,
                Mathf.RoundToInt(menu.SelectedValue));
        }
        return ParameterCondition.Bool(
            menu.ParameterName,
            isEnabled == (menu.SelectedValue != 0f));
    }
}
