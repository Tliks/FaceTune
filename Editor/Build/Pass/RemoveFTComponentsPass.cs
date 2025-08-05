using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal class RemoveFTComponentsPass : Pass<RemoveFTComponentsPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.remove-ft-components";
    public override string DisplayName => "Remove FT Components";

    protected override void Execute(BuildContext context)
    {
        foreach (var component in context.AvatarRootObject.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            Object.DestroyImmediate(component);
        }
    }
}

