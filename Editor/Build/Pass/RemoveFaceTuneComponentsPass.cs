namespace Aoyon.FaceTune.Build;

internal class RemoveFaceTuneComponentsPass : FaceTunePass<RemoveFaceTuneComponentsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.remove-facetune-components";
    public override string DisplayName => "Remove FaceTune Components";

    protected override void Execute(FaceTuneContext context)
    {
        foreach (var component in context.AvatarContext.Root.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            Object.DestroyImmediate(component);
        }
    }
}

