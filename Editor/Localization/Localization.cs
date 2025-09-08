using Aoyon.FaceTune.Gui;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune;

[InitializeOnLoad]
internal static class Localization
{
    private const string LocalizationFolderGUID = "a9a14ed168f25bc4dabf54f2e630fd78";
    private const string DefaultLanguage = "en-US";

    private static readonly Localizer _ndmfLocalizer;
    public static Localizer NdmfLocalizer => _ndmfLocalizer;

    public static event Action? OnLanguageChanged;

    static Localization()
    {
        _ndmfLocalizer = new Localizer(DefaultLanguage, () =>
        {
            var localizationFolderPath = AssetDatabase.GUIDToAssetPath(LocalizationFolderGUID);
            return new List<LocalizationAsset>
            {
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>(localizationFolderPath + "/" + "en-US.po"),
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>(localizationFolderPath + "/" + "ja-JP.po"),
            };
        });
        LanguagePrefs.RegisterLanguageChangeCallback(typeof(Localization), _ => OnLanguageChanged?.Invoke());
    }
    
    [MenuItem(MenuItems.ReloadLocalizationPath, false, MenuItems.ReloadLocalizationPriority)]
    public static void ReloadLocalization()
    {
        Localizer.ReloadLocalizations();
        OnLanguageChanged?.Invoke();
    }

    private const string TooltipSuffix = ":tooltip";
    public static string S(string key) => _ndmfLocalizer.GetLocalizedString(key);
    public static GUIContent G(string key)
    {
        var localized = _ndmfLocalizer.GetLocalizedString(key);
        if (_ndmfLocalizer.TryGetLocalizedString(key + TooltipSuffix, out var tooltip))
        {
            return new GUIContent(localized, tooltip);
        }
        return new GUIContent(localized);
    }

    public static void LocalizeUIElements(VisualElement element) => _ndmfLocalizer.LocalizeUIElements(element);

    public static void DrawLanguageSwitcher() => LanguageSwitcher.DrawImmediate();
    public static VisualElement CreateLanguageSwitcher() => new LanguageSwitcher();
}

internal static class LocalizationExtensions
{
    public static string GLS(this string key) => Localization.S(key);
    public static GUIContent GLG(this string key) => Localization.G(key);
}