using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ModifyEarlyDataPass : Pass<ModifyEarlyDataPass>
{
    public override string QualifiedName => "com.aoyon.facetune.modify-early-data";
    public override string DisplayName => "Modify Early Data";

    protected override void Execute(BuildContext context)
    {
        var modifiers = context.AvatarRootObject.GetInterfacesInChildFTComponents<IModifyEarlyData>(false);
        foreach (var modifier in modifiers)
        {
            modifier.Excute();
        }
    }
}
