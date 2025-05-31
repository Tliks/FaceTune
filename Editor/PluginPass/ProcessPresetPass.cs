using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ProcessPresetPass : Pass<ProcessPresetPass>
{
    public override string QualifiedName => "com.aoyon.facetune.process-preset";
    public override string DisplayName => "Process Preset";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.Extension<FTPassContext>()!;
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;
        var presetData = passContext.PatternData;
        if (presetData == null) throw new InvalidOperationException("PatternData is null");
        if (presetData.IsEmpty) return;

        // 単一のPresetを変換
        // presetData.ConvertSinglePresetToSingleExpressionPattern();
        
        foreach (var preset in presetData.GetAllPresets())
        {
            var presetCondition = preset.PresetCondition;
            var parameterName = presetCondition.ParameterName;
            var index = presetCondition.IntValue;

            // Preset以下の全ての条件にPresetIndexのParameterConditionを追加
            var expressionWithConditions = preset.AllExpressionWithConditions;
            foreach (var expressionWithCondition in expressionWithConditions)
            {
                var conditions = expressionWithCondition.Conditions.ToList();
                conditions.Add(presetCondition);
                expressionWithCondition.SetConditions(conditions);
            }

            // 排他制御のMenuItemを生成
            var menuTarget = preset.MenuTarget;
            if (menuTarget == null) continue;
            var menuItem = menuTarget.EnsureComponent<ModularAvatarMenuItem>();
            platform.PlatformSupport.EnsureMenuItemIsToggle(menuTarget.transform, menuItem);
            platform.PlatformSupport.AssignParameterName(menuTarget.transform, menuItem, parameterName); // Todo 上書きしていいかどうか。
            platform.PlatformSupport.AssignParameterValue(menuTarget.transform, menuItem, index);
        }
    }
}
