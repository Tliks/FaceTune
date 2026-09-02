using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune;

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

internal sealed class FaceTuneMenuResolver
{
    private readonly Transform _root;
    private readonly HashSet<Transform> _localFolders;
    private readonly HashSet<Transform> _externalFolders;

    internal FaceTuneMenuResolver(
        GameObject root,
        IEnumerable<Transform>? externalFolders = null)
    {
        _root = root.transform;
        _localFolders = root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.MenuKind == MenuComponent.Kind.Folder)
            .Select(menu => menu.transform)
            .ToHashSet();
        _externalFolders = (externalFolders ?? Array.Empty<Transform>()).ToHashSet();
    }

    public Transform Root => _root;

    public static string GetDisplayName(string? configuredName, string fallback)
        => string.IsNullOrWhiteSpace(configuredName) ? fallback : configuredName!;

    public Transform? ResolveDestination(Component owner, Transform? configuredTarget)
    {
        var validOwner = owner.DestroyedAsNull();
        if (validOwner == null || !IsInRoot(validOwner.transform))
            return null;

        var configured = configuredTarget.DestroyedAsNull();
        if (configured != null && !IsInRoot(configured))
            return null;

        if (configured != null && IsDestination(configured, validOwner)) return configured;

        var start = configured ?? validOwner.transform;
        var destination = _root.gameObject
            .GetComponentsInParentExcludingSelf<Transform>(start, true)
            .Reverse()
            .FirstOrDefault(candidate => IsDestination(candidate, validOwner));
        return destination ?? _root;
    }

    public void ValidateInstallTarget(Transform target, Component owner)
    {
        if (!IsInRoot(target))
        {
            throw new InvalidOperationException(
                $"Menu install target is outside the avatar: '{owner.name}'.");
        }
    }

    private bool IsDestination(Transform target, Component owner)
    {
        if (!_localFolders.Contains(target) && !_externalFolders.Contains(target)) return false;
        if (owner is MenuComponent { MenuKind: MenuComponent.Kind.Folder })
            return !target.IsChildOf(owner.transform);
        return true;
    }

    private bool IsInRoot(Transform target)
        => target == _root || target.IsChildOf(_root);

    public static Transform? ResolvePreviewTarget(Transform? explicitPreview, Component? owner)
    {
        var target = explicitPreview.DestroyedAsNull();
        var expressionOwner = (owner as ExpressionComponent).DestroyedAsNull();
        return target ?? expressionOwner?.transform.DestroyedAsNull();
    }

    public List<string> GetDefinedGroupNames()
    {
        var menuGroups = _root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.MenuKind == MenuComponent.Kind.Toggle
                           && !menu.UseExistingParameter
                           && menu.GenerateParameterGroup)
            .Select(menu => menu.GroupName);

        var behavior = new ExpressionBehaviorResolver();
        var expressionGroups = _root.GetComponentsInChildren<ExpressionComponent>(true)
            .Where(expression => expression.DirectMenuEnabled
                                 && behavior.Resolve(expression).WriteMode == ExpressionWriteMode.Blend)
            .Select(expression => expression.DirectMenuSettings.GroupName);

        return menuGroups.Concat(expressionGroups)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}

