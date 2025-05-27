using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class CommonConditionPass : Pass<CommonConditionPass>
{
    public override string QualifiedName => "com.aoyon.facetune.common-condition";
    public override string DisplayName => "Common Condition";

    protected override void Execute(BuildContext context)
    {
        // CommonConditionComponent
        var commonConditionComponents = context.AvatarRootObject.GetComponentsInChildren<CommonConditionComponent>(false);
        foreach (var commonConditionComponent in commonConditionComponents)
        {
            commonConditionComponent.Modify();
        }
    }
}
