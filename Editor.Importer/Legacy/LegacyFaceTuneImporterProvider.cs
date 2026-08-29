#pragma warning disable CS0618

using Aoyon.FaceTune.Importing;

namespace Aoyon.FaceTune.Importers.Legacy;

internal sealed class LegacyFaceTuneImporterProvider : IFaceTuneImporterProvider
{
    public FaceTuneImporterDescriptor Descriptor { get; } = new(
        "legacy-face-tune",
        75,
        "window.import.legacyFaceTune.title");

    [InitializeOnLoadMethod]
    private static void Register()
        => FaceTuneImporterRegistry.Register(new LegacyFaceTuneImporterProvider());

    public bool IsAvailable(GameObject avatarRoot)
        => avatarRoot.GetComponentsInChildren<LegacyFaceTuneTagComponent>(true).Length > 0;

    public IFaceTuneImportSession CreateSession(GameObject avatarRoot)
        => new Session(avatarRoot);

    private sealed class Session : IFaceTuneImportSession
    {
        private readonly GameObject _avatarRoot;

        public Session(GameObject avatarRoot)
        {
            _avatarRoot = avatarRoot;
        }

        public bool CanImport
            => _avatarRoot.GetComponentsInChildren<LegacyFaceTuneTagComponent>(true).Length > 0;

        public bool ApplyFaceRendererSettings => false;

        public void DrawConfiguration()
        {
        }

        public GameObject? Import(AvatarContext context, GameObject destination)
        {
            if (!CanImport) return null;
            return new LegacyFaceTuneImporter(context).Import(destination);
        }

        public void Dispose()
        {
        }
    }
}

#pragma warning restore CS0618
