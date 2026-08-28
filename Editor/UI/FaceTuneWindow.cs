using Aoyon.FaceTune.Importing;

namespace Aoyon.FaceTune.Gui;

internal sealed class FaceTuneWindow : EditorWindow
{
    private enum Mode
    {
        Configure,
        Import
    }

    private enum RecipeGroup
    {
        None,
        StandardHands,
        OneHand,
        BothHands,
        AdvancedBlend,
        Expression
    }

    private sealed record RecipeOption(FaceTunePatternKey Pattern, RecipeGroup Group);

    private static class RecipeCatalog
    {
        public static readonly RecipeGroup[] Groups =
        {
            RecipeGroup.None,
            RecipeGroup.StandardHands,
            RecipeGroup.OneHand,
            RecipeGroup.BothHands,
            RecipeGroup.AdvancedBlend,
            RecipeGroup.Expression
        };

        public static readonly RecipeOption[] All =
        {
            new(FaceTunePatternKey.LeftHandPriority, RecipeGroup.StandardHands),
            new(FaceTunePatternKey.RightHandPriority, RecipeGroup.StandardHands),
            new(FaceTunePatternKey.LeftHand, RecipeGroup.OneHand),
            new(FaceTunePatternKey.RightHand, RecipeGroup.OneHand),
            new(FaceTunePatternKey.BothHands, RecipeGroup.BothHands),
            new(FaceTunePatternKey.Blending, RecipeGroup.AdvancedBlend),
            new(FaceTunePatternKey.Expression, RecipeGroup.Expression)
        };

        public static readonly RecipeOption Default = All.Single(
            recipe => recipe.Pattern == FaceTunePatternKey.RightHandPriority);

        public static RecipeOption[] GetRecipes(RecipeGroup group)
            => All.Where(recipe => recipe.Group == group).ToArray();
    }

    private static class RecipeText
    {
        public static string GroupTitleKey(RecipeGroup group)
            => group switch
            {
                RecipeGroup.None => "window.configure.parts.none",
                RecipeGroup.StandardHands => "window.recipeGroup.standardHands.title",
                RecipeGroup.OneHand => "window.recipeGroup.oneHand.title",
                RecipeGroup.BothHands => "window.recipeGroup.bothHands.title",
                RecipeGroup.AdvancedBlend => "window.recipeGroup.advancedBlend.title",
                RecipeGroup.Expression => "window.recipeGroup.expression.title",
                _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
            };

        public static string GroupDescriptionKey(RecipeGroup group)
            => group switch
            {
                RecipeGroup.StandardHands => "window.recipeGroup.standardHands.description",
                RecipeGroup.OneHand => "window.recipeGroup.oneHand.description",
                RecipeGroup.BothHands => "window.recipeGroup.bothHands.description",
                RecipeGroup.AdvancedBlend => "window.recipeGroup.advancedBlend.description",
                RecipeGroup.Expression => "window.recipeGroup.expression.description",
                _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
            };

        public static string RecipeLabelKey(FaceTunePatternKey pattern)
            => pattern switch
            {
                FaceTunePatternKey.LeftHandPriority => "window.recipe.leftHandPriority.title",
                FaceTunePatternKey.RightHandPriority => "window.recipe.rightHandPriority.title",
                FaceTunePatternKey.LeftHand => "window.recipe.leftHand.title",
                FaceTunePatternKey.RightHand => "window.recipe.rightHand.title",
                FaceTunePatternKey.BothHands => "window.recipe.bothHands.title",
                FaceTunePatternKey.Blending => "window.recipe.blending.title",
                FaceTunePatternKey.Expression => "window.recipe.expression.title",
                _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null)
            };

        public static string VariantLabelKey(RecipeGroup group)
            => group == RecipeGroup.StandardHands
                ? "window.recipeVariant.priority.label"
                : "window.recipeVariant.hand.label";
    }

    private static readonly Vector2 WindowSize = new(420f, 450f);

    [SerializeField] private GameObject? _avatarRoot;
    private Mode _mode;
    private RecipeGroup _selectedRecipeGroup = RecipeGroup.StandardHands;
    private bool _addStandardSetup = true;
    private bool _enableDefaultExpression = true;
    private Vector2 _scroll;

    private RecipeOption? _selectedRecipe;
    private string? _selectedImporterId;
    private IFaceTuneImportSession? _importSession;
    private string? _statusKey;
    private string? _warningKey;



    public static void Open(GameObject avatarRoot)
    {
        var window = GetWindow<FaceTuneWindow>();
        window.titleContent = new GUIContent(FaceTuneConstants.Name);
        window.minSize = WindowSize;
        window.maxSize = WindowSize;
        window.SetAvatar(avatarRoot);
        window.OpenMode(Mode.Configure);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(FaceTuneConstants.Name);
        minSize = WindowSize;
        maxSize = WindowSize;
        Localization.OnLanguageChanged += Repaint;
        SelectDefaultRecipe();
    }

    private void OnDisable()
    {
        Localization.OnLanguageChanged -= Repaint;
        DisposeImportSession();
    }

    private void SetAvatar(GameObject? avatarRoot)
    {
        if (_avatarRoot == avatarRoot) return;

        _avatarRoot = avatarRoot;
        _mode = Mode.Configure;
        SelectDefaultRecipe();
        _addStandardSetup = true;
        _enableDefaultExpression = true;
        _selectedImporterId = null;
        _statusKey = null;
        _warningKey = null;
        DisposeImportSession();
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

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.Space(8f);

        if (_mode == Mode.Configure)
            DrawConfigure();
        else
            DrawImport();

        if (_statusKey != null || _warningKey != null)
        {
            EditorGUILayout.Space(8f);
            if (_statusKey != null)
                EditorGUILayout.HelpBox(_statusKey.LS(), MessageType.Info);
            if (_warningKey != null)
                EditorGUILayout.HelpBox(_warningKey.LS(), MessageType.Warning);
        }

        EditorGUILayout.Space(12f);
        EditorGUILayout.EndScrollView();
        DrawFooter();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8f);
        GUILayout.Label(FaceTuneConstants.Name, HeaderStyle, GUILayout.ExpandWidth(true));
        DrawLine();
    }

    private void DrawConfigure()
    {
        if (DrawOperationSelector()) return;
        var standardRoot = FaceTunePrefabOperations.FindStandardRoot(_avatarRoot!);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            DrawFoundation(standardRoot);

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            DrawRecipeSelection();

        var addAction = CanAddConfiguration(standardRoot) ? (Action)AddConfiguration : null;
        DrawPrimaryButton(
            standardRoot == null && _addStandardSetup
                ? "window.configure.add.button"
                : "window.configure.addParts.button",
            addAction);
    }

    private void DrawFoundation(GameObject? standardRoot)
    {
        DrawSectionHeader("window.configure.foundation.title");

        if (standardRoot != null)
        {
            GUILayout.Label("window.configure.foundation.existing".LS(), EditorStyles.miniLabel);
            return;
        }

        _addStandardSetup = EditorGUILayout.ToggleLeft(
            "window.configure.foundation.add.label".LS(),
            _addStandardSetup);

        if (_addStandardSetup)
        {
            _enableDefaultExpression = EditorGUILayout.ToggleLeft(
                "window.configure.default.label".LS(),
                _enableDefaultExpression);
            EditorGUILayout.HelpBox(
                "window.configure.default.description".LS(),
                MessageType.Info);
        }
    }

    private void DrawRecipeSelection()
    {
        DrawSectionHeader("window.configure.parts.title");

        var selectedIndex = Array.IndexOf(RecipeCatalog.Groups, _selectedRecipeGroup);
        var options = RecipeCatalog.Groups
            .Select(RecipeText.GroupTitleKey)
            .Select(key => key.LS())
            .ToArray();
        var nextIndex = EditorGUILayout.Popup(
            "window.configure.parts.type.label".LS(),
            selectedIndex,
            options);
        if (nextIndex != selectedIndex)
        {
            _selectedRecipeGroup = RecipeCatalog.Groups[nextIndex];
            _selectedRecipe = RecipeCatalog.GetRecipes(_selectedRecipeGroup).FirstOrDefault();
        }

        if (_selectedRecipeGroup == RecipeGroup.None)
            return;

        var recipes = RecipeCatalog.GetRecipes(_selectedRecipeGroup);
        if (recipes.Length > 1)
        {
            var variantIndex = Array.IndexOf(recipes, _selectedRecipe);
            if (variantIndex < 0) variantIndex = 0;
            var nextVariant = variantIndex;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(
                    RecipeText.VariantLabelKey(_selectedRecipeGroup).LS(),
                    EditorStyles.label);
                nextVariant = GUILayout.Toolbar(
                    variantIndex,
                    recipes.Select(recipe => RecipeText.RecipeLabelKey(recipe.Pattern).LS()).ToArray(),
                    GUILayout.ExpandWidth(true));
            }
            _selectedRecipe = recipes[nextVariant];
        }

        EditorGUILayout.HelpBox(
            RecipeText.GroupDescriptionKey(_selectedRecipeGroup).LS(),
            MessageType.Info);
    }

    private void SelectDefaultRecipe()
    {
        _selectedRecipeGroup = RecipeGroup.StandardHands;
        _selectedRecipe = RecipeCatalog.Default;
    }

    private bool CanAddConfiguration(GameObject? standardRoot)
        => _selectedRecipe != null || standardRoot == null && _addStandardSetup;

    private void AddConfiguration()
    {
        if (_avatarRoot == null) return;

        var existingStandardRoot = FaceTunePrefabOperations.FindStandardRoot(_avatarRoot);
        var addStandard = existingStandardRoot == null && _addStandardSetup;
        var recipe = _selectedRecipe;
        if (!addStandard && recipe == null) return;

        GameObject? lastCreated = null;
        RunUndo("Add FaceTune Configuration", () =>
        {
            var standardRoot = existingStandardRoot;
            if (addStandard)
                standardRoot = FaceTunePrefabOperations.AddStandardSetup(
                    _avatarRoot,
                    _enableDefaultExpression);

            if (recipe != null)
                lastCreated = FaceTunePrefabOperations.AddPattern(_avatarRoot, recipe.Pattern, standardRoot);

            var selected = lastCreated ?? standardRoot ?? _avatarRoot;
            Selection.activeObject = selected;
            EditorGUIUtility.PingObject(selected);
        });

        SelectDefaultRecipe();
        _statusKey = "window.configure.completed.message";
        _warningKey = null;
        Repaint();
    }

    private void DrawImport()
    {
        if (DrawOperationSelector()) return;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var providers = FaceTuneImporterRegistry.GetAvailable(_avatarRoot!);
            if (providers.Count == 0)
            {
                EditorGUILayout.HelpBox("window.import.none.message".LS(), MessageType.Info);
                return;
            }

            var selectedProvider = providers.FirstOrDefault(
                provider => provider.Descriptor.Id == _selectedImporterId) ?? providers[0];
            if (_selectedImporterId != selectedProvider.Descriptor.Id)
                SelectImporter(selectedProvider);

            var selectedIndex = Enumerable.Range(0, providers.Count)
                .First(index => providers[index] == selectedProvider);
            var nextIndex = EditorGUILayout.Popup(
                "window.import.source.label".LS(),
                selectedIndex,
                providers.Select(provider => provider.Descriptor.TitleKey.LS()).ToArray());
            if (nextIndex != selectedIndex)
            {
                SelectImporter(providers[nextIndex]);
                return;
            }

            _importSession ??= selectedProvider.CreateSession(_avatarRoot!);
            _importSession.DrawConfiguration();

            using (new EditorGUI.DisabledScope(!_importSession.CanImport))
                DrawPrimaryButton("window.import.button", RunImport);
        }
    }

    private void RunImport()
    {
        if (_importSession == null
            || !AvatarContext.TryGet(_avatarRoot!, out var avatarContext, out _))
        {
            EditorUtility.DisplayDialog(FaceTuneConstants.Name, "window.import.avatarInvalid.message".LS(), "OK");
            return;
        }

        GameObject? created = null;
        RunUndo("Import to FaceTune", () =>
        {
            var templateRoot = FaceTunePrefabOperations.AddStandardSetup(_avatarRoot!, true);
            created = _importSession.Import(avatarContext, templateRoot) ?? templateRoot;
        });

        if (created != null)
        {
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        _statusKey = "window.import.completed.message";
        _warningKey = "window.import.completed.warning";
        Repaint();
    }

    private void OpenMode(Mode mode)
    {
        if (_mode == mode)
        {
            _statusKey = null;
            _warningKey = null;
            return;
        }

        DisposeImportSession();
        _selectedImporterId = null;
        _mode = mode;
        _scroll = Vector2.zero;
        _statusKey = null;
        _warningKey = null;
        Repaint();
    }

    private void SelectImporter(IFaceTuneImporterProvider provider)
    {
        DisposeImportSession();
        _selectedImporterId = provider.Descriptor.Id;
        _importSession = provider.CreateSession(_avatarRoot!);
    }

    private void DisposeImportSession()
    {
        _importSession?.Dispose();
        _importSession = null;
    }

    private bool DrawOperationSelector()
    {
        var options = new[]
        {
            "window.configure.title".LS(),
            "window.import.title".LS()
        };
        var nextMode = EditorGUILayout.Popup(GUIContent.none, (int)_mode, options);
        EditorGUILayout.Space(8f);
        if (nextMode == (int)_mode) return false;

        OpenMode((Mode)nextMode);
        return true;
    }

    private static void DrawPrimaryButton(string key, Action? action)
    {
        EditorGUILayout.Space(8f);
        using var row = new EditorGUILayout.HorizontalScope();
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(action == null))
        {
            if (GUILayout.Button(key.LS(), GUILayout.MinWidth(220f), GUILayout.Height(30f)))
                action?.Invoke();
        }
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

    private static void DrawSectionHeader(string titleKey)
    {
        var rect = EditorGUILayout.GetControlRect(false, GUIHelper.ShurikenHeaderHeight);
        GUI.Box(rect, titleKey.LG(), GUIStyles.SectionHeader);
    }

    private static void DrawLine()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.3f));
    }

    private static void DrawFooter()
    {
        using var row = new EditorGUILayout.HorizontalScope();
        Localization.DrawLanguageSwitcher();
        GUILayout.Space(8f);
    }

    private static GUIStyle HeaderStyle => new(EditorStyles.boldLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = 18
    };
}
