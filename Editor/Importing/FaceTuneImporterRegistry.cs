namespace Aoyon.FaceTune.Importing;

internal sealed record FaceTuneImporterDescriptor(
    string Id,
    int Priority,
    string TitleKey,
    string DescriptionKey,
    string PostImportGuideKey,
    bool SourceIsUnchanged);

internal interface IFaceTuneImporterProvider
{
    FaceTuneImporterDescriptor Descriptor { get; }
    bool IsAvailable(GameObject avatarRoot);
    IFaceTuneImportSession CreateSession(GameObject avatarRoot);
}

internal interface IFaceTuneImportSession : IDisposable
{
    void DrawConfiguration();
    bool CanImport { get; }
    GameObject? Import(AvatarContext context, GameObject destination);
}

internal static class FaceTuneImporterRegistry
{
    private static readonly Dictionary<string, IFaceTuneImporterProvider> Providers = new(StringComparer.Ordinal);

    public static void Register(IFaceTuneImporterProvider provider)
        => Providers[provider.Descriptor.Id] = provider;

    public static IReadOnlyList<IFaceTuneImporterProvider> GetAvailable(GameObject avatarRoot)
        => Providers.Values
            .Where(provider => provider.IsAvailable(avatarRoot))
            .OrderBy(provider => provider.Descriptor.Priority)
            .ThenBy(provider => provider.Descriptor.Id, StringComparer.Ordinal)
            .ToArray();
}
