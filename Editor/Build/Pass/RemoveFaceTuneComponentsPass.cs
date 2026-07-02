using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal class RemoveFaceTuneComponentsPass : Pass<RemoveFaceTuneComponentsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.remove-facetune-components";
    public override string DisplayName => "Remove FaceTune Components";

    protected override void Execute(BuildContext context)
    {
        foreach (var component in context.AvatarRootObject.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            Object.DestroyImmediate(component);
        }
    }
}

