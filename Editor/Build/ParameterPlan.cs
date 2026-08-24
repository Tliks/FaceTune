namespace Aoyon.FaceTune.Build;

internal enum ParameterValueType
{
    Bool,
    Int,
    Float
}

internal sealed record ParameterItem(
    string Name,
    ParameterValueType Type,
    float DefaultValue,
    bool Synced,
    bool Saved);

internal sealed class ParameterPlan
{
    public IReadOnlyList<ParameterItem> Items { get; }

    public ParameterPlan(IEnumerable<ParameterItem> items)
    {
        Items = items.ToArray();
    }
}
