namespace Aoyon.FaceTune;

internal interface IHasObjectReferences
{
    void ResolveReferences();
}

internal interface ISettingProvider<T> where T : class
{
    (bool Enabled, T Value) Setting { get; }
}

internal interface ISettingProviderWithReference<T> : ISettingProvider<T> where T : class
{
    (SettingsReferenceMode Mode, FaceTuneTagComponent? Source) Reference { get; }
}

internal interface IExpressionDefinitionProvider
{
}

internal interface IExpressionDefinitionProviderWithReference : IExpressionDefinitionProvider
{
    SettingsReferenceMode DefinitionMode { get; }
    FaceTuneTagComponent? DefinitionSource { get; }
}
