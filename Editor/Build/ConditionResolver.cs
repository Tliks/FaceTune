using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal sealed class ConditionResolver
{
    private readonly GameObject _root;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly ParameterDomainRegistry _parameterDomains;

    public ConditionResolver(
        GameObject root,
        IMetabasePlatformSupport platformSupport,
        ParameterDomainRegistry parameterDomains)
    {
        _root = root;
        _platformSupport = platformSupport;
        _parameterDomains = parameterDomains;
    }

    private DnfCondition ResolveCondition(Condition condition)
    {
        var resolvedCases = condition.Cases
            .Select(ResolveConditionCase)
            .OfType<DnfCondition>()
            .ToList();

        return resolvedCases.Count == 0
            ? DnfCondition.Never
            : DnfCondition.Any(resolvedCases);
    }

    public DnfCondition Resolve(ExpressionComponent expression)
    {
        if (!expression.HasCondition) return DnfCondition.Never;

        var conditions = _root.GetComponentsInParentExcludingSelf<SettingsComponent>(expression, true)
            .Where(settings => settings.HasCondition)
            .Select(settings => settings.Condition);
        if (expression.Condition.Mode == ConditionSelection.Kind.Conditional)
            conditions = conditions.Append(expression.Condition.Condition);

        return DnfCondition.All(
            conditions.Select(condition => ResolveCondition(condition)));
    }

    public DnfCondition? Resolve(ConditionSelection? selection)
    {
        if (selection == null) return null;
        return selection.Mode == ConditionSelection.Kind.Always
            ? DnfCondition.Always
            : ResolveCondition(selection.Condition);
    }

    public DnfCondition? Resolve(Condition? condition)
        => condition == null ? null : ResolveCondition(condition);

    public DnfCondition? Resolve(object? condition)
    {
        if (condition == null) return null;

        return condition switch
        {
            HandGestureCondition handGesture =>
                _platformSupport.ResolveHandGestureCondition(handGesture, _parameterDomains),
            ParameterCondition parameter =>
                _platformSupport.ResolveParameterCondition(parameter, _parameterDomains),
            _ => throw new InvalidOperationException(
                $"Unsupported condition type: {condition?.GetType().FullName ?? "null"}")
        };
    }

    private DnfCondition? ResolveConditionCase(ConditionCase conditionCase)
    {
        var resolvedConditions = conditionCase.EnumerateConditions()
            .Select(Resolve)
            .OfType<DnfCondition>()
            .ToArray();
        return resolvedConditions.Length == 0
            ? null
            : DnfCondition.All(resolvedConditions);
    }
}
