using com.aoyon.facetune.platform;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;

namespace com.aoyon.facetune.build;

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
        ProcessFacialStyle(buildPassContext);
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
            var parameterName = platformSupport.GetParameterName(menuItem);
            if (string.IsNullOrWhiteSpace(parameterName)) continue;
            usedParameterNames.Add(parameterName);
        }

        var parameterTypes = GetParameterTypes(root);

        foreach (var menuItem in menuItems)
        {    
            using var _ = ListPool<ExpressionComponent>.Get(out var expressionComponents);
            menuItem.GetComponentsInChildren<ExpressionComponent>(true, expressionComponents);
            if (expressionComponents.Any() is false) continue;

            var menuItemType = platformSupport.GetMenuItemType(menuItem);

            switch (menuItemType)
            {
                case MenuItemType.Toggle:
                case MenuItemType.Button:
                    var parameterName = EnsureParameter(menuItem, usedParameterNames, platformSupport);
                    var parameterValue = platformSupport.GetParameterValue(menuItem);

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
                case MenuItemType.RadialPuppet:
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
            string parameterName = platformSupport.GetParameterName(menuItem);
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                parameterName = platformSupport.GetUniqueParameterName(menuItem, usedParameterNames, "toggle");
                usedParameterNames.Add(parameterName);
                platformSupport.SetParameterName(menuItem, parameterName);
            }
            return parameterName;
        }

        static string EnsureRadialParameter(ModularAvatarMenuItem menuItem, HashSet<string> usedParameterNames, IPlatformSupport platformSupport)
        {
            string parameterName = platformSupport.GetRadialParameterName(menuItem);
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                parameterName = platformSupport.GetUniqueParameterName(menuItem, usedParameterNames, "radial");
                usedParameterNames.Add(parameterName);
                platformSupport.SetRadialParameterName(menuItem, parameterName);
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
            platformSupport.SetMenuItemType(menuItem, MenuItemType.Toggle);
            platformSupport.SetParameterName(menuItem, Preset_Index_Parameter);  // Todo 上書きしていいかどうか。
            platformSupport.SetParameterValue(menuItem, presetIndex);
        }
    }

    private static void ProcessFacialStyle(BuildPassContext buildPassContext)
    {
        var root = buildPassContext.SessionContext.Root;
        var facialStyleComponents = root.GetComponentsInChildren<FacialStyleComponent>(true);
        foreach (var facialStyleComponent in facialStyleComponents)
        {
            // AsDefaultが有効な場合、低い優先度かつFacialStyleの影響を受ける空のExpressionを生成する
            // Presetなどで、切り替えた際に追加の条件を満たさない、デフォルト時にFacialStyleの影響を受けた表情を常時再生するような用途。
            if (facialStyleComponent.AsDefault)
            {
                var defaultExpression = new GameObject(facialStyleComponent.name);
                defaultExpression.transform.parent = facialStyleComponent.transform;
                defaultExpression.transform.SetAsFirstSibling();

                var defaultExpressionComponent = defaultExpression.AddComponent<ExpressionComponent>();
                defaultExpressionComponent.FacialSettings = FacialSettings.Keep with { EnableBlending = false };
            }
        }
    }

    private static void NormalizeData(BuildPassContext buildPassContext)
    {
        // Patternに属しないExpressionをそれぞれ単一のPatternとして扱うことでデータを正規化する
        var root = buildPassContext.SessionContext.Root;
        var expressionComponents = root.GetComponentsInChildren<ExpressionComponent>(true);
        foreach (var expressionComponent in expressionComponents)
        {
            if (expressionComponent.GetComponentInParentNullable<PatternComponent>() == null)
            {
                expressionComponent.gameObject.EnsureComponent<PatternComponent>();
            }
        }
    }
}
