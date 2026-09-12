namespace Aoyon.FaceTune.Build;

internal sealed class CreateParameterPlanPass : FaceTunePass<CreateParameterPlanPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.create-parameter-plan";
    public override string DisplayName => "Create Parameter Plan";

    protected override void Execute(FaceTuneContext context)
    {
        var plan = ParameterResolver.Resolve(context.AvatarContext.Root, validateForBuild: true);
        context.SetParameterPlan(plan);
    }
}
