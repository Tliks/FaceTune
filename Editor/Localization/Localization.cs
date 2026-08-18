using Aoyon.FaceTune.Gui;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using UnityEngine.UIElements;
using UnityEditorInternal;

namespace Aoyon.FaceTune;

internal static class Localization
{
    private const string LocalizationFolderGUID = "a9a14ed168f25bc4dabf54f2e630fd78";
    private const string DefaultLanguage = "en-US";
    private static readonly string[] SupportedLanguages = new string[] { "en-US", "ja-JP" };

    private static Localizer? _ndmfLocalizer;
    private static bool _missingAssetReloadAttempted;

    public static Localizer NdmfLocalizer => _ndmfLocalizer ??= InitializeLocalizer();

    public static event Action? OnLanguageChanged;

    static void Init()
    {
        LanguagePrefs.RegisterLanguageChangeCallback(typeof(Localization), _ => OnLanguageChanged?.Invoke());
    }

    private static Localizer InitializeLocalizer()
    {
        return new Localizer(DefaultLanguage, () =>
        {
            var localizationFolderPath = AssetDatabase.GUIDToAssetPath(LocalizationFolderGUID);
            var assets = new List<LocalizationAsset>();
            foreach (var language in SupportedLanguages)
            {
                var asset = AssetDatabase.LoadAssetAtPath<LocalizationAsset>(localizationFolderPath + "/" + language + ".po");
                if (asset == null)
                {
                    Debug.LogError($"Localization asset not found for language: {language}");
                    QueueMissingAssetReload();
                    continue;
                }
                assets.Add(asset);
            }
            return assets;
        });
    }
    
    [MenuItem(MenuItems.ReloadLocalizationPath, false, MenuItems.ReloadLocalizationPriority)]
    public static void ReloadLocalization()
    {
        Localizer.ReloadLocalizations();
        OnLanguageChanged?.Invoke();
        InternalEditorUtility.RepaintAllViews();
    }

    private static void QueueMissingAssetReload()
    {
        if (_missingAssetReloadAttempted) return;

        _missingAssetReloadAttempted = true;
        EditorApplication.delayCall += ReloadLocalization;
    }

    public static string S(string key) => NdmfLocalizer.GetLocalizedString(key);
    public static bool TryGetLocalizedString(string key, out string value) => NdmfLocalizer.TryGetLocalizedString(key, out value);
    public static GUIContent G(string key)
    {
        var localized = NdmfLocalizer.GetLocalizedString(key);
        var tooltipKey = key.EndsWith(".label", StringComparison.Ordinal)
            ? key[..^".label".Length] + ".tooltip"
            : key + ".tooltip";
        return NdmfLocalizer.TryGetLocalizedString(tooltipKey, out var tooltip)
            ? new GUIContent(localized, tooltip)
            : new GUIContent(localized);
    }

    public static void LocalizeUIElements(VisualElement element) => NdmfLocalizer.LocalizeUIElements(element);

    public static void DrawLanguageSwitcher() => LanguageSwitcher.DrawImmediate();
    public static VisualElement CreateLanguageSwitcher() => new LanguageSwitcher();
}

internal static class LocalizationExtensions
{
    public static string LS(this string key) => Localization.S(key);
    public static GUIContent LG(this string key) => Localization.G(key);
}
