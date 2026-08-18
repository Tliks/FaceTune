using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal class CreateExpressionPlanPass : FaceTunePass<CreateExpressionPlanPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.create-expression-plan";
    public override string DisplayName => "Create Expression Plan";

    protected override void Execute(FaceTuneContext context)
    {
        var settings = context.RequireSettings();
        context.SetExpressionPlan(ExpressionPlanBuilder.Build(
            context.AvatarContext,
            context.PlatformSupport,
            settings));
    }
}

internal static class ExpressionPlanBuilder
{
    public static ExpressionPlan Build(
        AvatarContext context,
        IMetabasePlatformSupport platformSupport,
        BuildSettings settings)
    {
        var components = context.Root.GetComponentsInChildren<ExpressionComponent>(true);
        
        var conditionResolver = new ConditionResolver(platformSupport, settings.ParameterDomains);
        var expressionBuilder = new ExpressionItemBuilder(context, platformSupport, settings, conditionResolver);

        var items = components
            .SelectMany(expressionBuilder.Build)
            .Where(item => !item.RawWhen.IsNever)
            .ToList();

        return new ExpressionPlan(items);
    }
}

internal sealed class ExpressionItemBuilder
{
    private readonly AvatarContext _avatarContext;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly ConditionResolver _conditionResolver;
    private readonly FaceTuneResolver _resolver;
    private readonly IReadOnlyList<BlendShapeWeightAnimation> _safeZeroBlendShapeAnimations;

    public ExpressionItemBuilder(
        AvatarContext avatarContext,
        IMetabasePlatformSupport platformSupport,
        BuildSettings settings,
        ConditionResolver conditionResolver)
    {
        _avatarContext = avatarContext;
        _platformSupport = platformSupport;
        _conditionResolver = conditionResolver;
        _resolver = new FaceTuneResolver(avatarContext.Root);
        _safeZeroBlendShapeAnimations = avatarContext.FaceRenderer
            .GetBlendShapeWeights(avatarContext.FaceMesh)
            .Where(shape => !settings.IsBlendShapeExcluded(shape.Name))
            .Select(shape => shape with { Weight = 0f })
            .ToBlendShapeAnimations()
            .ToArray();
    }

    public IEnumerable<ExpressionItem> Build(ExpressionComponent component)
    {
        var incomingAnimations = new BlendShapeWeightAnimationSet();
        _resolver.FacialData.AddIncoming(component, incomingAnimations, _avatarContext.BodyPath);
        var localAnimations = new BlendShapeWeightAnimationSet();
        _resolver.FacialData.AddLocal(component, localAnimations, _avatarContext.BodyPath);
        var animations = new BlendShapeWeightAnimationSet();
        if (component.WriteMode == ExpressionWriteMode.Replace)
        {
            animations.AddRange(_safeZeroBlendShapeAnimations);
            animations.AddRange(incomingAnimations);
        }
        animations.AddRange(localAnimations);

        yield return BuildItem(
            component,
            component.name,
            incomingAnimations,
            animations,
            _resolver.Priority.Get(component),
            _conditionResolver.Resolve(component));

        var directCondition = component.DirectMenuSettings.GeneratedCondition;
        if (!component.DirectMenuEnabled || directCondition == null) yield break;

        var priority = _resolver.Priority.Get(component);
        yield return BuildItem(
            component,
            $"{component.name} (Direct Menu)",
            incomingAnimations,
            animations,
            new PrioritySettings
            {
                Priority = priority.Priority + component.DirectMenuSettings.PriorityOffset
            },
            _conditionResolver.Resolve(directCondition) ?? DnfCondition.Never);
    }

    private ExpressionItem BuildItem(
        ExpressionComponent component,
        string name,
        BlendShapeWeightAnimationSet incomingAnimations,
        BlendShapeWeightAnimationSet animations,
        PrioritySettings priority,
        DnfCondition when)
        => new(
            component.transform,
            name,
            incomingAnimations,
            animations,
            component.WriteMode,
            ResolveMultiFrame(component.MultiFrame),
            component.AllowEyeBlink,
            component.AllowLipSync,
            _resolver.EyeBlink.Get(component),
            _resolver.LipSync.Get(component),
            _resolver.Transition.Get(component),
            priority,
            when);

    private MultiFrameSettings ResolveMultiFrame(MultiFrameSettings settings)
    {
        var result = new MultiFrameSettings
        {
            MultiFrameMode = settings.MultiFrameMode,
            TriggerHand = settings.TriggerHand,
            ParameterName = settings.ParameterName
        };
        if (result.MultiFrameMode != MultiFrameSettings.Kind.Trigger)
            return result;

        var parameter = _platformSupport.ResolveGestureWeightParameter(result.TriggerHand);
        if (string.IsNullOrEmpty(parameter))
            result.MultiFrameMode = MultiFrameSettings.Kind.Default;
        else
        {
            result.MultiFrameMode = MultiFrameSettings.Kind.Parameter;
            result.ParameterName = parameter!;
        }
        return result;
    }

}
