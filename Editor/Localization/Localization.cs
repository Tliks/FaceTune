using Aoyon.FaceTune.Gui;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune;

internal static class Localization
{
    private const string LocalizationFolderGUID = "a9a14ed168f25bc4dabf54f2e630fd78";
    private const string DefaultLanguage = "en-US";
    private static readonly string[] SupportedLanguages = new string[] { "en-US", "ja-JP" };

    private static Localizer? _ndmfLocalizer;
    public static Localizer NdmfLocalizer => _ndmfLocalizer ??= InitializeLocalizer();

    public static event Action? OnLanguageChanged;

    [InitializeOnLoadMethod]
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
    }

    private const string TooltipSuffix = ":tooltip";
    public static string S(string key) => NdmfLocalizer.GetLocalizedString(key);
    public static GUIContent G(string key)
    {
        var localized = NdmfLocalizer.GetLocalizedString(key);
        if (NdmfLocalizer.TryGetLocalizedString(key + TooltipSuffix, out var tooltip))
        {
            return new GUIContent(localized, tooltip);
        }
        return new GUIContent(localized);
    }

    public static void LocalizeUIElements(VisualElement element) => NdmfLocalizer.LocalizeUIElements(element);

    public static void DrawLanguageSwitcher() => LanguageSwitcher.DrawImmediate();
    public static VisualElement CreateLanguageSwitcher() => new LanguageSwitcher();
}

internal static class LocalizationExtensions
{
    public static string S(this string key) => Localization.S(key);
    public static GUIContent G(this string key) => Localization.G(key);
}