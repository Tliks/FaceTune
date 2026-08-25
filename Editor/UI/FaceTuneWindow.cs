using Aoyon.FaceTune.Importing;

namespace Aoyon.FaceTune.Gui;

internal sealed class FaceTuneWindow : EditorWindow
{
    private enum Page
    {
        Home,
        StandardSetup,
        Add,
        Import,
        Result
    }

    [SerializeField] private GameObject? _avatarRoot;
    [SerializeField] private Page _page;
    [SerializeField] private bool _includeDefaultExpression = true;
    [SerializeField] private bool _importAddStandard = true;
    [SerializeField] private Vector2 _scroll;

    [NonSerialized] private FaceTuneRecipe? _selectedRecipe;
    [NonSerialized] private string? _selectedImporterId;
    [NonSerialized] private IFaceTuneImportSession? _importSession;
    [NonSerialized] private GameObject? _resultObject;
    [NonSerialized] private string? _resultTitleKey;
    [NonSerialized] private string? _resultGuideKey;

    public static void Open(GameObject avatarRoot)
    {
        var window = GetWindow<FaceTuneWindow>();
        window.titleContent = new GUIContent(FaceTuneConstants.Name);
        window.minSize = new Vector2(460f, 420f);
        window.SetAvatar(avatarRoot);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(FaceTuneConstants.Name);
        minSize = new Vector2(460f, 420f);
        Localization.OnLanguageChanged += Repaint;
    }

    private void OnDisable()
    {
        Localization.OnLanguageChanged -= Repaint;
        DisposeImportSession();
    }

    private void SetAvatar(GameObject avatarRoot)
    {
        if (_avatarRoot == avatarRoot) return;
        _avatarRoot = avatarRoot;
        Navigate(Page.Home);
    }

    private void OnGUI()
    {
        DrawHeader();
        if (_avatarRoot == null)
        {
            EditorGUILayout.HelpBox("window.avatarMissing.message".LS(), MessageType.Info);
            DrawFooter();
            return;
        }

        DrawNavigation();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.Space(8f);
        switch (_page)
        {
            case Page.Home: DrawHome(); break;
            case Page.StandardSetup: DrawStandardSetup(); break;
            case Page.Add: DrawAdd(); break;
            case Page.Import: DrawImport(); break;
            case Page.Result: DrawResult(); break;
            default: throw new ArgumentOutOfRangeException();
        }
        EditorGUILayout.Space(12f);
        EditorGUILayout.EndScrollView();
        DrawFooter();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(10f);
            GUILayout.Label(FaceTuneConstants.Name, HeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("window.avatar.label".LS(), EditorStyles.miniLabel);
            GUILayout.Label(_avatarRoot == null ? "—" : _avatarRoot.name, EditorStyles.boldLabel);
            GUILayout.Space(10f);
        }
        EditorGUILayout.Space(5f);
        DrawLine();
    }

    private void DrawNavigation()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(_page == Page.Home))
            {
                if (GUILayout.Button("window.home.button".LS(), EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    Navigate(Page.Home);
            }
            GUILayout.Label(GetPageTitleKey().LS(), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawHome()
    {
        DrawPageTitle("window.home.title", "window.home.description");
        var hasStandard = FaceTuneRecipeOperations.FindStandardRoot(_avatarRoot!) != null;
        DrawActionCard(
            "window.standard.title",
            "window.standard.description",
            !hasStandard ? "window.recommended.badge" : null,
            () => Navigate(Page.StandardSetup));
        DrawActionCard(
            "window.add.title",
            "window.add.description",
            null,
            () => Navigate(Page.Add));

        var providers = FaceTuneImporterRegistry.GetAvailable(_avatarRoot!);
        DrawActionCard(
            "window.import.title",
            "window.import.description",
            providers.Count == 0 ? "window.import.none.badge" : null,
            () => Navigate(Page.Import));
    }

    private void DrawStandardSetup()
    {
        DrawPageTitle("window.standard.title", "window.standard.introduction");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("window.standard.contents.title".LS(), EditorStyles.boldLabel);
            GUILayout.Label("window.standard.contents.settings".LS(), WrappedLabel);
            GUILayout.Label("window.standard.contents.option".LS(), WrappedLabel);
            GUILayout.Label("window.standard.contents.default".LS(), WrappedLabel);
        }

        EditorGUILayout.Space(6f);
        _includeDefaultExpression = EditorGUILayout.ToggleLeft(
            "window.standard.includeDefault.label".LS(),
            _includeDefaultExpression);
        if (_includeDefaultExpression)
            EditorGUILayout.HelpBox("window.standard.default.notice".LS(), MessageType.Info);
        else
            EditorGUILayout.HelpBox("window.standard.noDefault.notice".LS(), MessageType.Info);

        if (FaceTuneRecipeOperations.FindStandardRoot(_avatarRoot!) != null)
            EditorGUILayout.HelpBox("window.standard.existing.warning".LS(), MessageType.Warning);

        DrawPrimaryButton("window.standard.add.button", AddStandardSetup);
    }

    private void DrawAdd()
    {
        DrawPageTitle("window.add.title", "window.add.introduction");
        DrawRecipeCategory(FaceTuneRecipeCategory.Expression, "window.category.expression");
        DrawRecipeCategory(FaceTuneRecipeCategory.Pattern, "window.category.pattern");
        DrawRecipeCategory(FaceTuneRecipeCategory.Control, "window.category.control");

        if (_selectedRecipe == null) return;
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label(_selectedRecipe.TitleKey.LS(), EditorStyles.boldLabel);
            GUILayout.Label(_selectedRecipe.DescriptionKey.LS(), WrappedLabel);
            var destination = GetRecipeDestinationDescription(_selectedRecipe);
            EditorGUILayout.Space(3f);
            GUILayout.Label($"{"window.destination.label".LS()}: {destination}", EditorStyles.miniLabel);
            if (GUILayout.Button("window.add.button".LS(), GUILayout.Height(28f))) AddSelectedRecipe();
        }
    }

    private void DrawRecipeCategory(FaceTuneRecipeCategory category, string titleKey)
    {
        GUILayout.Label(titleKey.LS(), SectionTitleStyle);
        var recipes = FaceTuneRecipes.All.Where(recipe => recipe.Category == category).ToArray();
        const int columns = 2;
        for (var i = 0; i < recipes.Length; i += columns)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            for (var column = 0; column < columns; column++)
            {
                var index = i + column;
                if (index >= recipes.Length)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }
                var recipe = recipes[index];
                var selected = recipe == _selectedRecipe;
                var label = selected ? $"✓ {recipe.TitleKey.LS()}" : recipe.TitleKey.LS();
                if (GUILayout.Button(label, GUILayout.MinHeight(30f))) _selectedRecipe = recipe;
            }
        }
        EditorGUILayout.Space(5f);
    }

    private void DrawImport()
    {
        DrawPageTitle("window.import.title", "window.import.introduction");
        var providers = FaceTuneImporterRegistry.GetAvailable(_avatarRoot!);
        if (providers.Count == 0)
        {
            EditorGUILayout.HelpBox("window.import.none.message".LS(), MessageType.Info);
            return;
        }

        if (_selectedImporterId == null)
        {
            foreach (var provider in providers)
            {
                DrawActionCard(
                    provider.Descriptor.TitleKey,
                    provider.Descriptor.DescriptionKey,
                    null,
                    () => SelectImporter(provider));
            }
            return;
        }

        var selectedProvider = providers.FirstOrDefault(provider => provider.Descriptor.Id == _selectedImporterId);
        if (selectedProvider == null)
        {
            SelectImporter(null);
            return;
        }

        if (GUILayout.Button("window.import.changeSource.button".LS(), GUILayout.Width(150f)))
        {
            SelectImporter(null);
            return;
        }

        EditorGUILayout.Space(5f);
        GUILayout.Label(selectedProvider.Descriptor.TitleKey.LS(), SectionTitleStyle);
        _importSession ??= selectedProvider.CreateSession(_avatarRoot!);
        _importSession.DrawConfiguration();

        var standardRoot = FaceTuneRecipeOperations.FindStandardRoot(_avatarRoot!);
        if (!selectedProvider.Descriptor.CreatesStandaloneSetup)
        {
            EditorGUILayout.Space(8f);
            GUILayout.Label("window.import.foundation.title".LS(), SectionTitleStyle);
            if (standardRoot != null)
            {
                EditorGUILayout.HelpBox("window.import.foundation.existing".LS(), MessageType.Info);
            }
            else
            {
                _importAddStandard = EditorGUILayout.ToggleLeft(
                    "window.import.foundation.addStandard.label".LS(),
                    _importAddStandard);
                GUILayout.Label(
                    (_importAddStandard
                        ? "window.import.foundation.addStandard.description"
                        : "window.import.foundation.contentOnly.description").LS(),
                    WrappedLabel);
                if (_importAddStandard)
                {
                    EditorGUI.indentLevel++;
                    _includeDefaultExpression = EditorGUILayout.ToggleLeft(
                        "window.standard.includeDefault.label".LS(),
                        _includeDefaultExpression);
                    EditorGUI.indentLevel--;
                    if (_includeDefaultExpression)
                        EditorGUILayout.HelpBox("window.standard.default.notice".LS(), MessageType.Info);
                }
            }
        }

        if (selectedProvider.Descriptor.SourceIsUnchanged)
            EditorGUILayout.HelpBox("window.import.sourceUnchanged.notice".LS(), MessageType.Warning);

        using (new EditorGUI.DisabledScope(!_importSession.CanImport))
            DrawPrimaryButton("window.import.button", () => RunImport(selectedProvider, standardRoot));
    }

    private void DrawResult()
    {
        DrawPageTitle("window.result.title", _resultTitleKey ?? "window.result.default");
        EditorGUILayout.HelpBox((_resultGuideKey ?? "window.result.default").LS(), MessageType.Info);
        if (_resultObject != null)
        {
            GUILayout.Label($"{"window.result.created.label".LS()}: {_resultObject.name}", EditorStyles.boldLabel);
            if (GUILayout.Button("window.result.select.button".LS(), GUILayout.Height(26f)))
            {
                Selection.activeObject = _resultObject;
                EditorGUIUtility.PingObject(_resultObject);
            }
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("window.result.addAnother.button".LS())) Navigate(Page.Add);
            if (GUILayout.Button("window.home.button".LS())) Navigate(Page.Home);
        }
    }

    private void AddStandardSetup()
    {
        RunUndo("Add FaceTune Standard Setup", () =>
        {
            var created = FaceTuneRecipeOperations.AddStandardSetup(_avatarRoot!, _includeDefaultExpression);
            ShowResult(created, "window.result.standard.title", _includeDefaultExpression
                ? "window.result.standard.guide"
                : "window.result.standardWithoutDefault.guide");
        });
    }

    private void AddSelectedRecipe()
    {
        if (_selectedRecipe == null) return;
        RunUndo("Add FaceTune Part", () =>
        {
            var created = FaceTuneRecipeOperations.AddRecipe(_avatarRoot!, _selectedRecipe);
            ShowResult(created, "window.result.recipe.title", _selectedRecipe.GuideKey);
        });
    }

    private void RunImport(IFaceTuneImporterProvider provider, GameObject? standardRoot)
    {
        if (_importSession == null
            || !AvatarContext.TryGet(_avatarRoot!, out var avatarContext, out _))
        {
            EditorUtility.DisplayDialog(FaceTuneConstants.Name, "window.import.avatarInvalid.message".LS(), "OK");
            return;
        }

        RunUndo("Import to FaceTune", () =>
        {
            var destination = _avatarRoot!;
            if (!provider.Descriptor.CreatesStandaloneSetup)
            {
                destination = standardRoot;
                if (destination == null && _importAddStandard)
                    destination = FaceTuneRecipeOperations.AddStandardSetup(_avatarRoot!, _includeDefaultExpression);
                destination ??= _avatarRoot!;
            }

            var created = _importSession.Import(avatarContext, destination);
            ShowResult(
                created ?? destination,
                "window.result.import.title",
                provider.Descriptor.PostImportGuideKey);
        });
    }

    private void SelectImporter(IFaceTuneImporterProvider? provider)
    {
        DisposeImportSession();
        _selectedImporterId = provider?.Descriptor.Id;
        if (provider != null) _importSession = provider.CreateSession(_avatarRoot!);
    }

    private void ShowResult(GameObject created, string titleKey, string guideKey)
    {
        _resultObject = created;
        _resultTitleKey = titleKey;
        _resultGuideKey = guideKey;
        Selection.activeObject = created;
        EditorGUIUtility.PingObject(created);
        Navigate(Page.Result, preserveResult: true);
    }

    private void Navigate(Page page, bool preserveResult = false)
    {
        if (page != Page.Import) DisposeImportSession();
        _page = page;
        _scroll = Vector2.zero;
        if (!preserveResult && page != Page.Result)
        {
            _resultObject = null;
            _resultTitleKey = null;
            _resultGuideKey = null;
        }
        Repaint();
    }

    private void DisposeImportSession()
    {
        _importSession?.Dispose();
        _importSession = null;
        if (_page != Page.Import) _selectedImporterId = null;
    }

    private string GetRecipeDestinationDescription(FaceTuneRecipe recipe)
    {
        var standardRoot = FaceTuneRecipeOperations.FindStandardRoot(_avatarRoot!);
        if (standardRoot == null) return _avatarRoot!.name;
        if (recipe.Category != FaceTuneRecipeCategory.Control) return standardRoot.name;
        var option = standardRoot.GetComponentsInChildren<MenuComponent>(true)
            .FirstOrDefault(menu => menu.MenuKind == MenuComponent.Kind.Folder
                                 && menu.gameObject.name == "Option");
        return option == null ? standardRoot.name : $"{standardRoot.name}/{option.name}";
    }

    private void DrawActionCard(
        string titleKey,
        string descriptionKey,
        string? badgeKey,
        Action action)
    {
        using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(titleKey.LS(), SectionTitleStyle);
            GUILayout.FlexibleSpace();
            if (badgeKey != null) GUILayout.Label(badgeKey.LS(), EditorStyles.miniLabel);
        }
        GUILayout.Label(descriptionKey.LS(), WrappedLabel);
        if (GUILayout.Button("window.open.button".LS(), GUILayout.Height(24f))) action();
    }

    private static void DrawPageTitle(string titleKey, string descriptionKey)
    {
        GUILayout.Label(titleKey.LS(), PageTitleStyle);
        GUILayout.Label(descriptionKey.LS(), WrappedLabel);
        EditorGUILayout.Space(8f);
    }

    private static void DrawPrimaryButton(string key, Action action)
    {
        EditorGUILayout.Space(8f);
        using var row = new EditorGUILayout.HorizontalScope();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(key.LS(), GUILayout.MinWidth(180f), GUILayout.Height(30f))) action();
    }

    private static void RunUndo(string name, Action action)
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
        try
        {
            action();
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
    }

    private static void DrawLine()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
    }

    private static void DrawFooter()
    {
        DrawLine();
        using var row = new EditorGUILayout.HorizontalScope();
        GUILayout.FlexibleSpace();
        Localization.DrawLanguageSwitcher();
        GUILayout.Space(8f);
    }

    private string GetPageTitleKey() => _page switch
    {
        Page.Home => "window.home.title",
        Page.StandardSetup => "window.standard.title",
        Page.Add => "window.add.title",
        Page.Import => "window.import.title",
        Page.Result => "window.result.title",
        _ => "window.home.title"
    };

    private static GUIStyle HeaderStyle => new(EditorStyles.boldLabel) { fontSize = 15 };
    private static GUIStyle PageTitleStyle => new(EditorStyles.boldLabel) { fontSize = 18 };
    private static GUIStyle SectionTitleStyle => new(EditorStyles.boldLabel) { fontSize = 13 };
    private static GUIStyle WrappedLabel => new(EditorStyles.label) { wordWrap = true };
}
