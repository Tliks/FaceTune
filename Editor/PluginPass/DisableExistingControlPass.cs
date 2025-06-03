using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class DisableExistingControlPass : Pass<DisableExistingControlPass>
{
    public override string QualifiedName => "com.aoyon.facetune.disable-existing-control";
    public override string DisplayName => "Disable Existing Control";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.Extension<FTPassContext>()!;
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;

        if (sessionContext.Root.GetComponentsInChildren<DisableExistingControlComponent>(true).Where(c => c.OverrideBlendShapes).Any())
        {
            platform.PlatformSupport.DisableExistingControl(passContext);
        }
    }
}
