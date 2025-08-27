using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using Newtonsoft.Json;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui;

[InitializeOnLoad]
internal static class Localization
{
    private const string DefaultLanguage = "en-us";
    private static readonly Dictionary<string, string> LanguageToGUID = new()
    {
        { "en-US", "2e2a97f8dd9377d4f9d96e335a1993b1" },
        { "ja-JP", "0a33e6283053ed343b112905f9ffaa34" }
    };
    private static readonly string[] SupportedLanguages = LanguageToGUID.Keys.ToArray();

    private static Localizer _ndmfLocalizer;

    public static event Action? OnLanguageChanged;

    static Localization()
    {
        _ndmfLocalizer = new Localizer(DefaultLanguage, () =>
        {
            return SupportedLanguages.Select(l =>
            {
                Func<string, string?> fetcher;
                if (!TryLoadStringTable(l, out var stringTable)) fetcher = (k) => null;
                else fetcher = key => stringTable.TryGetValue(key, out var value) ? value : null;
                return (l, fetcher);
            }).ToList();
        });
        LanguagePrefs.RegisterLanguageChangeCallback(typeof(Localization), _ => OnLanguageChanged?.Invoke());
    }
    
    private static bool TryLoadStringTable(string language, [NotNullWhen(true)]out Dictionary<string, string>? stringTable)
    {
        stringTable = null;
        try
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(LanguageToGUID[language]));
            if (textAsset == null) throw new Exception($"String table for {language} not found");
            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text);
            if (deserialized == null) throw new Exception($"Failed to deserialize string table for {language}");
            stringTable = deserialized;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load string table for {language}: {e.Message}");
            return false;
        }
    }

    [MenuItem(MenuItems.ReloadLocalizationPath, false, MenuItems.ReloadLocalizationPriority)]
    public static void ReloadLocalization()
    {
        Localizer.ReloadLocalizations();
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

    public static void LanguageSwitcherGUI() => LanguageSwitcher.DrawImmediate();
    public static VisualElement CreateLanguageSwitcherUI() => new LanguageSwitcher();
}