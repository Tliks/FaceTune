namespace Aoyon.FaceTune;

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

    public static Transform? ResolveIconPreviewTarget(Transform? configured, Component? owner)
    {
        var target = configured.DestroyedAsNull();
        var expression = (owner as ExpressionComponent).DestroyedAsNull();
        return target ?? expression?.transform.DestroyedAsNull();
    }

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
}
