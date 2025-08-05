using Aoyon.FaceTune.Platform;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

// Hierarchy上のデータを弄るパスは基本的にここに置く
// なお、PatternDataを必要とする場合は後続のパス。
internal class ModifyHierarchyPass : Pass<ModifyHierarchyPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.modify-hierarchy";
    public override string DisplayName => "Modify Hierarchy";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;
        Excute(buildPassContext);
    }

    internal static void Excute(BuildPassContext buildPassContext)
    {
        // Condition
        NegotiateMAMenuItem(buildPassContext);
        ProcessPreset(buildPassContext);
        // Expression
        // 
        // Pattern
        NormalizeData(buildPassContext);
    }

    private static void NegotiateMAMenuItem(BuildPassContext buildPassContext)
    {
        var root = buildPassContext.SessionContext.Root;
        var platformSupport = buildPassContext.PlatformSupport;

        var usedParameterNames = new HashSet<string>();
        var menuItems = root.GetComponentsInChildren<ModularAvatarMenuItem>(true);
        foreach (var menuItem in menuItems)
        {
            var parameterName = menuItem.PortableControl.Parameter;
            if (string.IsNullOrWhiteSpace(parameterName)) continue;
            usedParameterNames.Add(parameterName);
        }

        var parameterTypes = GetParameterTypes(root);

        foreach (var menuItem in menuItems)
        {    
            using var _ = ListPool<ExpressionComponent>.Get(out var expressionComponents);
            menuItem.GetComponentsInChildren<ExpressionComponent>(true, expressionComponents);
            if (expressionComponents.Any() is false) continue;

            var menuItemType = menuItem.PortableControl.Type;

            switch (menuItemType)
            {
                case PortableControlType.Toggle:
                case PortableControlType.Button:
                    var parameterName = EnsureParameter(menuItem, usedParameterNames, platformSupport);
                    var parameterValue = menuItem.PortableControl.Value;

                    ParameterCondition? condition = null;
                    if (parameterTypes.TryGetValue(parameterName, out var parameterType))
                    {
                        switch (parameterType)
                        {
                            case ParameterType.Bool:
                                var boolValue = parameterValue != 0;
                                condition = ParameterCondition.Bool(parameterName, boolValue);
                                break;
                            case ParameterType.Int:
                                condition = ParameterCondition.Int(parameterName, ComparisonType.Equal, (int)parameterValue);
                                break;
                            case ParameterType.Float:
                                condition = ParameterCondition.Float(parameterName, ComparisonType.Equal, (float)parameterValue);
                                break;
                            default:
                                break;
                        }
                    }
                    else if (menuItem.automaticValue)
                    {
                        var boolValue = parameterValue != 0;
                        condition = ParameterCondition.Bool(parameterName, boolValue);
                    }
                    
                    if (condition == null) continue;

                    var conditionComponent = menuItem.gameObject.AddComponent<ConditionComponent>();
                    conditionComponent.ParameterConditions.Add(condition);
                    break;
                case PortableControlType.RadialPuppet:
                    var radialParameterName = EnsureRadialParameter(menuItem, usedParameterNames, platformSupport);
                    foreach (var expressionComponent in expressionComponents)
                    {
                        var settings = expressionComponent.ExpressionSettings;
                        expressionComponent.ExpressionSettings = settings with { LoopTime = false, MotionTimeParameterName = radialParameterName };
                    }
                    break;
                default:
                    break;
            }
        }

        static string EnsureParameter(ModularAvatarMenuItem menuItem, HashSet<string> usedParameterNames, IPlatformSupport platformSupport)
        {
            string parameterName = menuItem.PortableControl.Parameter;
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                parameterName = GetUniqueParameterName(menuItem.gameObject.name, usedParameterNames, "toggle");
                usedParameterNames.Add(parameterName);
                menuItem.PortableControl.Parameter = parameterName;
            }
            return parameterName;
        }

        static string EnsureRadialParameter(ModularAvatarMenuItem menuItem, HashSet<string> usedParameterNames, IPlatformSupport platformSupport)
        {
            string parameterName = menuItem.PortableControl.SubParameters.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                parameterName = GetUniqueParameterName(menuItem.gameObject.name, usedParameterNames, "radial");
                usedParameterNames.Add(parameterName);
                menuItem.PortableControl.SubParameters = ImmutableList.Create(parameterName);
            }
            var parameters = menuItem.gameObject.EnsureComponent<ModularAvatarParameters>();
            parameters.parameters.Add(new ParameterConfig()
            {
                nameOrPrefix = parameterName,
                syncType = ParameterSyncType.Float,
                defaultValue = 0,
            });
            return parameterName;
        }

        static Dictionary<string, ParameterType> GetParameterTypes(GameObject root)
        {
            var parameterTypes = new Dictionary<string, ParameterType>();
            var parameters = root.GetComponentsInChildren<ModularAvatarParameters>(true)
                .SelectMany(x => x.parameters)
                .Select(x => (x.nameOrPrefix, x.syncType));
            foreach (var (name, syncType) in parameters)
            {
                switch (syncType)
                {
                    case ParameterSyncType.Int:
                        parameterTypes[name] = ParameterType.Int;
                        break;
                    case ParameterSyncType.Float:
                        parameterTypes[name] = ParameterType.Float;
                        break;
                    case ParameterSyncType.Bool:
                        parameterTypes[name] = ParameterType.Bool;
                        break;
                    default:
                        continue;
                }
            }
            return parameterTypes;
        }
        
        static string GetUniqueParameterName(string baseName, HashSet<string> usedParameterNames, string suffix)
        {
            baseName = baseName.Replace(" ", "_");
            baseName = baseName.Replace(".", "_");
            var parameterName = $"{FaceTuneConsts.ParameterPrefix}/{baseName}/{suffix}";
            int index = 1;
            while (usedParameterNames.Contains(parameterName))
            {
                parameterName = $"{FaceTuneConsts.ParameterPrefix}/{baseName}_{index}/{suffix}";
                index++;
            }
            return parameterName;
        }
    }

    private const string Preset_Index_Parameter = $"{FaceTuneConsts.ParameterPrefix}/PresetIndex";
    private static void ProcessPreset(BuildPassContext buildPassContext)
    {
        var platformSupport = buildPassContext.PlatformSupport;
        var presetComponents = buildPassContext.SessionContext.Root.GetComponentsInChildren<PresetComponent>(true);
        var presetIndex = 0;   
        foreach (var presetComponent in presetComponents)
        {
            // indexの条件を生成
            var presetCondition = ParameterCondition.Int(Preset_Index_Parameter, ComparisonType.Equal, presetIndex++);

            // 配下のExpressionに大してその条件を設定
            var conditionComponent = presetComponent.gameObject.AddComponent<ConditionComponent>();
            conditionComponent.ParameterConditions.Add(presetCondition);

            // 条件を発火させるMenuItemを設定
            var menuTarget = presetComponent.GetMenuTarget();
            var menuItem = menuTarget.EnsureComponent<ModularAvatarMenuItem>();
            menuItem.PortableControl.Type = PortableControlType.Toggle;
            menuItem.PortableControl.Parameter = Preset_Index_Parameter;  // Todo 上書きしていいかどうか。
            menuItem.PortableControl.Value = presetIndex;
        }
    }

    private static void NormalizeData(BuildPassContext buildPassContext)
    {
        // Patternに属しないExpressionをそれぞれ単一のPatternとして扱うことでデータを正規化する
        var root = buildPassContext.SessionContext.Root;
        var expressionComponents = root.GetComponentsInChildren<ExpressionComponent>(true);
        foreach (var expressionComponent in expressionComponents)
        {
            if (expressionComponent.GetComponentInParentNullable<PatternComponent>(true) == null)
            {
                expressionComponent.gameObject.EnsureComponent<PatternComponent>();
            }
        }
    }
}
