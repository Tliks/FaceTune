namespace Aoyon.FaceTune;

internal readonly record struct IntParameterDomain(int MinValue, int MaxValue)
{
    public bool IsValid => MinValue <= MaxValue;

    public bool Contains(int value)
    {
        return MinValue <= value && value <= MaxValue;
    }
}

internal sealed record ParameterDomainRegistry
{
    private ImmutableDictionary<string, IntParameterDomain> IntDomainOverrides { get; init; }
        = ImmutableDictionary.Create<string, IntParameterDomain>(StringComparer.Ordinal);
    private IntParameterDomain? DefaultIntDomain { get; init; }

    public static ParameterDomainRegistry Empty { get; } = new();

    public ParameterDomainRegistry WithDefaultIntDomain(IntParameterDomain domain)
    {
        return domain.IsValid ? this with { DefaultIntDomain = domain } : this;
    }

    public ParameterDomainRegistry WithIntDomainOverride(string parameterName, IntParameterDomain domain)
    {
        return string.IsNullOrWhiteSpace(parameterName) || !domain.IsValid
            ? this
            : this with { IntDomainOverrides = IntDomainOverrides.SetItem(parameterName, domain) };
    }

    public bool TryGetIntDomain(string parameterName, out IntParameterDomain domain)
    {
        if (IntDomainOverrides.TryGetValue(parameterName, out domain)) return true;
        if (DefaultIntDomain is { } defaultDomain)
        {
            domain = defaultDomain;
            return true;
        }

        domain = default;
        return false;
    }
}
