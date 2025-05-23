using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ResolveReferencesPass : Pass<ResolveReferencesPass>
{
    public override string QualifiedName => "com.aoyon.facetune.resolve-references";
    public override string DisplayName => "Resolve References";

    protected override void Execute(BuildContext context)
    {
        var interfaces = context.AvatarRootObject
            .GetInterfacesInChildFTComponents<IHasObjectReferences>(true);

        foreach (var hasObjectReferences in interfaces)
        {
            hasObjectReferences.ResolveReferences();
        }
    }
}
