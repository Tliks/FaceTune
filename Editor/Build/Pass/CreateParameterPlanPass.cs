namespace Aoyon.FaceTune.Build;

internal sealed class CreateParameterPlanPass : FaceTunePass<CreateParameterPlanPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.create-parameter-plan";
    public override string DisplayName => "Create Parameter Plan";

    protected override void Execute(FaceTuneContext context)
    {
        context.SetParameterPlan(ParameterPlanBuilder.Build(context.AvatarContext.Root));
    }
}

internal static class ParameterPlanBuilder
{
    public static ParameterPlan Build(GameObject root)
        => new(ParameterResolver.ResolveParameters(root).Select(declaration =>
            new ParameterItem(
                declaration.Name,
                declaration.Type,
                declaration.DefaultValue,
                declaration.Synced,
                declaration.Saved)));
}
