namespace Aoyon.FaceTune.Build;

internal enum ParameterValueType
{
    Bool,
    Int,
    Float
}

internal sealed record ParameterItem(
    FaceTuneTagComponent Source,
    string Name,
    ParameterValueType Type,
    float DefaultValue,
    bool Synced,
    bool Saved);

internal readonly record struct ParameterBinding(
    string Name,
    float SelectedValue,
    bool GenerateParameterGroup,
    string GroupName,
    bool Synced,
    bool Saved);

internal sealed class ParameterPlan
{
    private readonly IReadOnlyDictionary<FaceTuneTagComponent, ParameterBinding> _bindings;

    public IReadOnlyList<ParameterItem> Items { get; }
    public IReadOnlyDictionary<string, IntParameterDomain> IntDomains { get; }

    public ParameterPlan(
        IEnumerable<ParameterItem> items,
        IReadOnlyDictionary<FaceTuneTagComponent, ParameterBinding> bindings,
        IReadOnlyDictionary<string, IntParameterDomain> intDomains)
    {
        Items = items.ToArray();
        _bindings = bindings;
        IntDomains = intDomains;
    }

    public ParameterBinding GetBinding(FaceTuneTagComponent source)
        => _bindings.TryGetValue(source, out var binding)
            ? binding
            : throw new InvalidOperationException($"Parameter binding was not resolved for '{source.name}'.");

    public IEnumerable<KeyValuePair<FaceTuneTagComponent, ParameterBinding>> Bindings
        => _bindings;
}
