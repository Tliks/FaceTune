#if FACETUNE_FACE_EMO

using Aoyon.FaceTune.Importing;
using Suzuryg.FaceEmo.Components;
using Suzuryg.FaceEmo.Components.Data;

namespace Aoyon.FaceTune.Importers.FaceEmo;

internal sealed class FaceEmoImporterProvider : IFaceTuneImporterProvider
{
    public FaceTuneImporterDescriptor Descriptor { get; } = new(
        "face-emo",
        50,
        "window.import.faceEmo.title",
        "window.import.faceEmo.description",
        "window.result.faceEmoImport.guide",
        true,
        true);

    [InitializeOnLoadMethod]
    private static void Register()
        => FaceTuneImporterRegistry.Register(new FaceEmoImporterProvider());

    public bool IsAvailable(GameObject avatarRoot)
        => FindSources(avatarRoot).Count > 0;

    public IFaceTuneImportSession CreateSession(GameObject avatarRoot)
        => new Session(avatarRoot);

    private static List<FaceEmoLauncherComponent> FindSources(GameObject avatarRoot)
        => Resources.FindObjectsOfTypeAll<FaceEmoLauncherComponent>()
            .Where(source => source.gameObject.scene.IsValid())
            .Where(source => source.AV3Setting?.TargetAvatar is Component target
                             && target.gameObject == avatarRoot)
            .Where(source => source.GetComponent<MenuRepositoryComponent>()?.SerializableMenu != null)
            .OrderBy(source => source.gameObject.name, StringComparer.Ordinal)
            .ToList();

    private sealed class Session : IFaceTuneImportSession
    {
        private readonly List<FaceEmoLauncherComponent> _sources;
        private int _selectedSource;
        private string _outputFolder;

        public Session(GameObject avatarRoot)
        {
            _sources = FindSources(avatarRoot);
            _outputFolder = $"Assets/FaceTune/FaceEmo Import/{avatarRoot.name}";
        }

        public bool CanImport => _sources.Count > 0
                                 && (_outputFolder == "Assets"
                                     || _outputFolder.StartsWith("Assets/", StringComparison.Ordinal));

        public void DrawConfiguration()
        {
            if (_sources.Count == 1)
            {
                EditorGUILayout.ObjectField(
                    "window.import.faceEmo.source.label".LS(),
                    _sources[0].gameObject,
                    typeof(GameObject),
                    true);
            }
            else
            {
                _selectedSource = EditorGUILayout.Popup(
                    "window.import.faceEmo.source.label".LS(),
                    _selectedSource,
                    _sources.Select(source => source.gameObject.name).ToArray());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField(
                    "window.import.faceEmo.outputFolder.label".LS(),
                    _outputFolder);
                if (GUILayout.Button("window.import.faceEmo.outputFolder.button".LS(), GUILayout.Width(80f)))
                {
                    var selected = EditorUtility.OpenFolderPanel(
                        "window.import.faceEmo.outputFolder.label".LS(),
                        Application.dataPath,
                        string.Empty);
                    if (selected.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                        _outputFolder = "Assets" + selected.Substring(Application.dataPath.Length).Replace('\\', '/');
                }
            }
        }

        public GameObject? Import(AvatarContext context, GameObject destination)
        {
            if (!CanImport) return null;

            var source = _sources[_selectedSource];
            var menu = source.GetComponent<MenuRepositoryComponent>().SerializableMenu;
            var transitionSeconds = (float)(source.AV3Setting?.TransitionDurationSeconds ?? 0d);
            EnsureFolder(_outputFolder);
            var root = new GameObject("FaceTune (Imported from FaceEmo)");
            Undo.RegisterCreatedObjectUndo(root, "Import FaceEmo");
            root.transform.SetParent(context.Root.transform, false);

            new FaceEmoImporter(context, source, menu, transitionSeconds, _outputFolder).Import(root);
            AssetDatabase.SaveAssets();

            Selection.activeObject = root;
            EditorGUIUtility.PingObject(root);
            return root;
        }

        private static void EnsureFolder(string path)
        {
            var current = "Assets";
            foreach (var part in path.Substring("Assets".Length).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        public void Dispose()
        {
        }
    }
}

#endif
