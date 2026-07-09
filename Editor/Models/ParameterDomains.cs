namespace Aoyon.FaceTune;

internal readonly record struct IntParameterDomain(int MinValue, int MaxValue)
{
    public bool IsValid => MinValue <= MaxValue;

    public bool Contains(int value)
    {
        return MinValue <= value && value <= MaxValue;
    }
}

internal sealed class ParameterDomainRegistry
{
    private readonly Dictionary<string, IntParameterDomain> intDomainOverrides = new(StringComparer.Ordinal);
    private IntParameterDomain? defaultIntDomain;

    public static ParameterDomainRegistry Empty { get; } = new();

    public ParameterDomainRegistry()
    {
    }

    public ParameterDomainRegistry(ParameterDomainRegistry source)
    {
        defaultIntDomain = source.defaultIntDomain;
        foreach (var (parameterName, domain) in source.intDomainOverrides)
        {
            intDomainOverrides.Add(parameterName, domain);
        }
    }

    public void SetDefaultIntDomain(IntParameterDomain domain)
    {
        if (!domain.IsValid) return;
        defaultIntDomain = domain;
    }

    public void SetIntDomainOverride(string parameterName, IntParameterDomain domain)
    {
        if (string.IsNullOrWhiteSpace(parameterName) || !domain.IsValid) return;
        intDomainOverrides[parameterName] = domain;
    }

    public bool TryGetIntDomain(string parameterName, out IntParameterDomain domain)
    {
        if (intDomainOverrides.TryGetValue(parameterName, out domain)) return true;
        if (defaultIntDomain.HasValue)
        {
            domain = defaultIntDomain.Value;
            return true;
        }

        domain = default;
        return false;
    }
}
