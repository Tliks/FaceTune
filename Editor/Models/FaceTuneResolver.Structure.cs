using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;
internal readonly record struct ScopedValue<T>(T Value, SettingsComponent? Owner);

internal sealed class ScopedValueResolver<T> where T : class
{
    private readonly GameObject root;
    private readonly Func<SettingsComponent, T?> getSettings;
    private readonly Func<T> getDefault;
    private readonly ComputeContext context;

    public ScopedValueResolver(
        GameObject root,
        Func<SettingsComponent, T?> getSettings,
        Func<T> getDefault,
        ComputeContext? context)
    {
        this.root = root;
        this.getSettings = getSettings;
        this.getDefault = getDefault;
        this.context = context ?? ComputeContext.NullContext;
    }

    public ScopedValue<T> GetIncoming(Component target)
    {
        var value = getDefault();
        SettingsComponent? owner = null;
        foreach (var settings in context.GetComponentsInParentExcludingSelf<SettingsComponent>(root, target, true))
        {
            if (getSettings(settings) is not { } resolved) continue;
            value = resolved;
            owner = settings;
        }
        return new ScopedValue<T>(value, owner);
    }
}

internal sealed class SettingValueResolver<TValue> where TValue : class
{
    private readonly ComputeContext context;
    private readonly Func<Component, TValue, TValue> snapshot;

    public SettingValueResolver(
        Func<Component, TValue, TValue> snapshot,
        ComputeContext? context = null)
    {
        this.snapshot = snapshot;
        this.context = context ?? ComputeContext.NullContext;
    }

    public TValue? Resolve(ISettingProvider<TValue> provider)
        => Resolve(provider, new HashSet<Component>());

    private TValue? Resolve(ISettingProvider<TValue> provider, HashSet<Component> path)
    {
        var component = (Component)provider;
        if (!path.Add(component)) return null;
        try
        {
            var value = context.Observe(
                component,
                current =>
                {
                    var setting = ((ISettingProvider<TValue>)current).Setting;
                    var reference = (current as ISettingProviderWithReference<TValue>)?.Reference;
                    return (
                        setting.Enabled,
                        Value: snapshot(current, setting.Value),
                        FollowReference: reference?.Mode == SettingsReferenceMode.Reference,
                        Source: reference?.Source);
                },
                (left, right) => left.Enabled == right.Enabled
                                 && Equals(left.Value, right.Value)
                                 && left.FollowReference == right.FollowReference
                                 && left.Source == right.Source);
            if (!value.Enabled) return null;
            if (!value.FollowReference) return value.Value;
            return value.Source is ISettingProvider<TValue> source
                ? Resolve(source, path)
                : null;
        }
        finally
        {
            path.Remove(component);
        }
    }
}
