using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune.Build;

internal class GenerateParameterPass : FaceTunePass<GenerateParameterPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.generate-parameter";
    public override string DisplayName => "Generate Parameter";

    private const string GeneratedParameterRootName = "FaceTune Generated Parameter";

    protected override void Execute(FaceTuneContext context)
    {
        var generatedRoot = new GameObject(GeneratedParameterRootName);
        generatedRoot.transform.SetParent(context.AvatarContext.Root.transform, false);

        var parameters = generatedRoot.AddComponent<ModularAvatarParameters>();
        var configs = new Dictionary<string, ParameterConfig>();
        
        // FaceTuenのパラメータはMenu以外で現状自動生成するものはない
        var menuComponents = context.AvatarContext.Root.GetComponentsInChildren<MenuComponent>(true);
        if (menuComponents.Length > 0)
        {
            AddMenuParameters(menuComponents, configs);
        }

        parameters.parameters.AddRange(configs.Values);
    }

    private static void AddMenuParameters(MenuComponent[] menus, Dictionary<string, ParameterConfig> configs)
    {
        var exclusiveDefaults = ResolveExclusiveDefaults(menus);
        foreach (var menu in menus)
        {
            var parameterName = menu.ParameterName;
            if (string.IsNullOrWhiteSpace(parameterName)) continue;

            var syncType = menu.Kind switch
            {
                MenuItemKind.Radial => ParameterSyncType.Float,
                MenuItemKind.Toggle when menu.ExclusiveToggleGroup.IsEnabled => ParameterSyncType.Int,
                MenuItemKind.Toggle => ParameterSyncType.Bool,
                _ => ParameterSyncType.NotSynced
            };

            if (configs.ContainsKey(parameterName)) continue;
            configs.Add(parameterName, new ParameterConfig
            {
                nameOrPrefix = parameterName,
                syncType = syncType,
                saved = true,
                defaultValue = GetDefaultValue(menu, exclusiveDefaults),
                hasExplicitDefaultValue = true
            });
        }
    }

    private static Dictionary<string, float> ResolveExclusiveDefaults(IEnumerable<MenuComponent> menus)
    {
        var result = new Dictionary<string, float>();
        foreach (var group in menus
            .Where(menu => menu.Kind == MenuItemKind.Toggle && menu.ExclusiveToggleGroup.IsEnabled && menu.DefaultSelected)
            .GroupBy(menu => menu.ParameterName))
        {
            var defaults = group.ToArray();
            if (defaults.Length > 1)
            {
                LocalizedLog.Warning("Log:warning:GenerateMenuPass:MultipleDefaultSelectedMenu", null, defaults);
            }

            result[group.Key] = defaults[0].ExclusiveToggleGroup.Value;
        }

        return result;
    }

    private static float GetDefaultValue(MenuComponent menu, IReadOnlyDictionary<string, float> exclusiveDefaults)
    {
        if (menu.Kind == MenuItemKind.Toggle && menu.ExclusiveToggleGroup.IsEnabled)
        {
            return exclusiveDefaults.GetValueOrDefault(menu.ParameterName, 0f);
        }

        if (menu.Kind == MenuItemKind.Toggle && menu.DefaultSelected) return 1f;
        return 0f;
    }
}
