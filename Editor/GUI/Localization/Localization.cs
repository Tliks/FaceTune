using System.IO;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using Newtonsoft.Json;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui;

[InitializeOnLoad]
internal static class Localization
{
    private const string LocalizationFolderGUID = "a9a14ed168f25bc4dabf54f2e630fd78";
    private static string LocalizationFolderPath => AssetDatabase.GUIDToAssetPath(LocalizationFolderGUID);
    private const string DefaultLanguage = "en-US";

    private static string[]? _supportedLanguages;
    private static readonly Dictionary<string, Dictionary<string, string>> _languageToStringTable = new();

    private static readonly Localizer _ndmfLocalizer;
    public static event Action? OnLanguageChanged;

    static Localization()
    {
        _ndmfLocalizer = new Localizer(DefaultLanguage, () =>
        {
            return GetSupportedLanguages().Select(l =>
            {
                Func<string, string?> fetcher;
                if (!TryGetStringTable(l, out var stringTable)) fetcher = (k) => null;
                else fetcher = key => stringTable.TryGetValue(key, out var value) ? value : null;
                return (l, fetcher);
            }).ToList();
        });
        LanguagePrefs.RegisterLanguageChangeCallback(typeof(Localization), _ => OnLanguageChanged?.Invoke());
    }

    private static string[] GetSupportedLanguages()
    {
        if (_supportedLanguages != null) return _supportedLanguages;

        if (Directory.Exists(LocalizationFolderPath))
        {
            _supportedLanguages = Directory.GetFiles(LocalizationFolderPath, "*.json", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToArray();
        }
        else
        {
            Debug.LogError($"Localization folder not found: {LocalizationFolderPath}");
            _supportedLanguages = Array.Empty<string>();
        }
        return _supportedLanguages.ToArray();
    }
    
    private static bool TryGetStringTable(string language, [NotNullWhen(true)] out Dictionary<string, string>? stringTable)
    {
        stringTable = null;
        try
        {
            if (_languageToStringTable.TryGetValue(language, out var cached))
            {
                stringTable = cached;
                return true;
            }

            var filePath = LocalizationFolderPath + "/" + language + ".json";
            var json = File.ReadAllText(filePath);
            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (deserialized == null) throw new Exception($"Failed to deserialize string table for {language}");
            stringTable = deserialized;
            _languageToStringTable[language] = deserialized;
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
        _supportedLanguages = null;
        _languageToStringTable.Clear();
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