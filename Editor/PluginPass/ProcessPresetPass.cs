using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ProcessPresetPass : Pass<ProcessPresetPass>
{
    public override string QualifiedName => "com.aoyon.facetune.process-preset";
    public override string DisplayName => "Process Preset";

    internal const string Peset_Index_Parameter = "FaceTune_PresetIndex";
    protected override void Execute(BuildContext context)
    {
        var root = context.AvatarRootObject;
        var presetComponents = root.GetComponentsInChildren<PresetComponent>(false);
        if (presetComponents.Length <= 1) return;

        var index = 1; // intパラメーターの初期値 // Todo:Parametersで明示的に指定すべき
        foreach (var presetComponent in presetComponents)
        {
            // 排他制御のMenuItemを生成
            var menuItem = presetComponent.gameObject.EnsureComponent<ModularAvatarMenuItem>();
            platform.PlatformSupport.EnsureMenuItemIsToggle(root.transform, menuItem);
            platform.PlatformSupport.AssignParameterName(root.transform, menuItem, Peset_Index_Parameter); // Todo 上書きしていいかどうか。
            platform.PlatformSupport.AssignParameterValue(root.transform, menuItem, index);

            // Preset以下の全ての条件にPresetIndexを追加
            var condition = presetComponent.gameObject.AddComponent<CommonConditionComponent>();
            condition.AllChildren = true;
            condition.ParameterConditions.Add(new ParameterCondition(Peset_Index_Parameter, IntComparisonType.Equal, index));
            
            index++;
        }
    }
}
