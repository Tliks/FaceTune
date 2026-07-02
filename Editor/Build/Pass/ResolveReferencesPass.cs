namespace Aoyon.FaceTune.Build;

internal class ResolveReferencesPass : FaceTunePass<ResolveReferencesPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.resolve-references";
    public override string DisplayName => "Resolve References";

    protected override void Execute(FaceTuneContext context)
    {
        var interfaces = context.AvatarContext.Root
            .GetInterfacesInChildFaceTuneComponents<IHasObjectReferences>(true);

        foreach (var hasObjectReferences in interfaces)
        {
            hasObjectReferences.ResolveReferences();
        }
    }
}
