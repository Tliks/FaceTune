namespace Aoyon.FaceTune;

internal interface IHasObjectReferences
{
    void ResolveReferences();
}

internal interface IFaceTuneProvider
{
    Component Component { get; }
}

internal interface ISettingProvider<T> : IFaceTuneProvider where T : class
{
    (bool Enabled, T Value) Setting { get; }
}

internal interface ISettingProviderWithReference<T> : ISettingProvider<T> where T : class
{
    (SettingsReferenceMode Mode, FaceTuneTagComponent? Source) Reference { get; }
}

internal interface IExpressionDefinitionProvider : IFaceTuneProvider
{
}

internal interface IExpressionDefinitionProviderWithReference : IExpressionDefinitionProvider
{
    (SettingsReferenceMode Mode, FaceTuneTagComponent? Source) Reference { get; }
}
