namespace Aoyon.FaceTune;

/// <summary>
/// Resolves optional expression settings from one FaceTune and its Group Settings
/// ancestors. It owns all hierarchy and Transform-reference traversal so callers
/// only select the slot they need.
/// </summary>
internal sealed class EffectiveExpressionSettingsResolver
{
    private readonly IReadOnlyList<Scope> _scopes;

    public EffectiveExpressionSettingsResolver(GameObject root, ExpressionComponent expression)
    {
        var scopes = new List<Scope> { new(expression, expression) };
        var current = expression.transform;
        while (current != null)
        {
            foreach (var group in current.GetComponents<GroupSettingsComponent>())
                scopes.Add(new(group, group));
            if (current.gameObject == root) break;
            current = current.parent;
        }
        _scopes = scopes;
    }

    public IReadOnlyList<GroupSettingsComponent> Groups
        => _scopes.Skip(1).Select(scope => (GroupSettingsComponent)scope.Owner).ToArray();

    public EffectiveSetting<TSetting>? Get<TSetting>(
        Func<IExpressionSettingsSource, IReadOnlyList<TSetting>> select,
        string settingName)
        where TSetting : class
    {
        foreach (var scope in _scopes)
        {
            var setting = GetSingle(select(scope.Source), scope.Owner, settingName);
            if (setting != null) return new EffectiveSetting<TSetting>(scope.Owner, setting);
        }
        return null;
    }

    public TValue ResolveReference<TSetting, TValue>(
        EffectiveSetting<TSetting> effective,
        Func<IExpressionSettingsSource, IReadOnlyList<TSetting>> select,
        string settingName)
        where TSetting : class, ISettingReference<TValue>
    {
        var setting = effective.Value;
        if (setting.SourceMode == SettingSourceMode.Direct) return setting.DirectValue;
        if (setting.Source == null)
            throw new InvalidOperationException($"{settingName} setting on '{effective.Owner.name}' references no Transform.");

        var referenced = setting.Source.GetComponents<Component>()
            .OfType<IExpressionSettingsSource>()
            .SelectMany(select)
            .ToArray();
        if (referenced.Length != 1)
            throw new InvalidOperationException(
                $"{settingName} setting on '{effective.Owner.name}' must resolve to exactly one {settingName} setting on '{setting.Source.name}'.");

        var target = referenced[0];
        if (target.SourceMode != SettingSourceMode.Direct)
            throw new InvalidOperationException($"{settingName} setting on '{effective.Owner.name}' cannot reference another reference setting.");
        return target.DirectValue;
    }

    private static TSetting? GetSingle<TSetting>(
        IReadOnlyList<TSetting> settings,
        Component owner,
        string settingName)
        where TSetting : class
    {
        return settings.Count switch
        {
            0 => null,
            1 => settings[0],
            _ => throw new InvalidOperationException(
                $"{owner.GetType().Name} on '{owner.name}' has multiple {settingName} settings.")
        };
    }

    private sealed record Scope(Component Owner, IExpressionSettingsSource Source);
}

internal readonly record struct EffectiveSetting<TSetting>(Component Owner, TSetting Value) where TSetting : class;
