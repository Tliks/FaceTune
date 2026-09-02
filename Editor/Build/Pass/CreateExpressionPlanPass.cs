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
        
        var conditionResolver = new ConditionResolver(context.Root, platformSupport, settings.ParameterDomains);
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
    private readonly ExpressionResolver _resolver;
    private readonly BuildSettings _settings;

    public ExpressionItemBuilder(
        AvatarContext avatarContext,
        IMetabasePlatformSupport platformSupport,
        BuildSettings settings,
        ConditionResolver conditionResolver)
    {
        _avatarContext = avatarContext;
        _platformSupport = platformSupport;
        _conditionResolver = conditionResolver;
        _resolver = new ExpressionResolver(avatarContext.Root);
        _settings = settings;
    }

    public IEnumerable<ExpressionItem> Build(ExpressionComponent component)
    {
        var expression = _resolver.Resolve(component, _avatarContext.BodyPath);

        var incomingFacialAnimations = expression.IncomingFacial.Clone();
        RemoveProhibitedAnimations(
            incomingFacialAnimations,
            FaceTuneWriteKind.FacialData);

        var localFacialAnimations = expression.DefinitionFacial.Clone();
        RemoveProhibitedAnimations(
            localFacialAnimations,
            FaceTuneWriteKind.FacialData);

        var nonFacialAnimations = expression.NonFacial;
        var eyeBlink = ResolveEyeBlink(expression.EyeBlink);
        var lipSync = ResolveLipSync(expression.LipSync);
        var transition = expression.Transition;
        var priority = expression.Priority;

        yield return BuildItem(
            component,
            component.name,
            incomingFacialAnimations,
            localFacialAnimations,
            nonFacialAnimations,
            eyeBlink,
            lipSync,
            transition,
            priority,
            expression,
            _conditionResolver.Resolve(component));

        var directCondition = component.DirectMenuSettings.GeneratedCondition;
        if (!component.DirectMenuEnabled || directCondition == null) yield break;

        yield return BuildItem(
            component,
            $"{component.name} (Direct Menu)",
            incomingFacialAnimations,
            localFacialAnimations,
            nonFacialAnimations,
            eyeBlink,
            lipSync,
            transition,
            new PrioritySettings
            {
                Priority = priority.Priority + component.DirectMenuSettings.PriorityOffset
            },
            expression,
            _conditionResolver.Resolve(directCondition) ?? DnfCondition.Never);
    }

    private ExpressionItem BuildItem(
        ExpressionComponent component,
        string name,
        BlendShapeWeightAnimationSet incomingFacialAnimations,
        BlendShapeWeightAnimationSet localFacialAnimations,
        ResolvedNonFacialAnimationSet nonFacialAnimations,
        EyeBlinkSettings eyeBlink,
        LipSyncSettings lipSync,
        TransitionSettings transition,
        PrioritySettings priority,
        ResolvedExpression expression,
        DnfCondition when)
        => new(
            component.transform,
            name,
            incomingFacialAnimations,
            localFacialAnimations,
            nonFacialAnimations,
            expression.WriteMode,
            ResolveMultiFrame(expression.MultiFrame),
            expression.AllowEyeBlink,
            expression.AllowLipSync,
            eyeBlink,
            lipSync,
            transition,
            priority,
            when);

    private EyeBlinkSettings ResolveEyeBlink(EyeBlinkSettings source)
    {
        var result = new EyeBlinkSettings
        {
            EyeBlinkMode = source.EyeBlinkMode,
            IntervalSeconds = source.IntervalSeconds,
            SimpleDurationsSeconds = source.SimpleDurationsSeconds,
            SimpleBlinkBlendShapes = source.SimpleBlinkBlendShapes
                .Where(shape => CanWrite(
                    FaceTuneWriteKind.EyeBlinkAnimation,
                    shape.Name))
                .ToList(),
            SimpleConflictPreventionBlendShapes = source.SimpleConflictPreventionBlendShapes
                .Where(shape => CanWrite(
                    FaceTuneWriteKind.FacialData,
                    shape.Name))
                .ToList(),
            Animations = source.Animations
                .Where(animation => CanWrite(
                    FaceTuneWriteKind.EyeBlinkAnimation,
                    animation.Name))
                .ToList()
        };
        return result;
    }

    private LipSyncSettings ResolveLipSync(LipSyncSettings source)
    {
        return new LipSyncSettings
        {
            CancellerBlendShapes = source.CancellerBlendShapes
                .Where(shape => CanWrite(FaceTuneWriteKind.FacialData, shape.Name))
                .ToList()
        };
    }

    private bool CanWrite(FaceTuneWriteKind writeKind, string name)
        => _settings.CanWriteBlendShape(writeKind, name);

    private void RemoveProhibitedAnimations(
        BlendShapeWeightAnimationSet animations,
        FaceTuneWriteKind writeKind)
    {
        var prohibited = animations
            .Where(animation => !CanWrite(writeKind, animation.Name))
            .Select(animation => animation.Name)
            .ToArray();
        animations.RemoveRange(prohibited);
    }

    private MultiFrameSettings ResolveMultiFrame(MultiFrameSettings settings)
    {
        var result = new MultiFrameSettings
        {
            MultiFrameMode = settings.MultiFrameMode,
            TriggerHand = settings.TriggerHand,
            ParameterName = settings.ParameterName
        };
        if (result.MultiFrameMode == MultiFrameSettings.Kind.Menu)
        {
            if (settings.MenuSource == null)
                result.MultiFrameMode = MultiFrameSettings.Kind.Default;
            else
            {
                result.MultiFrameMode = MultiFrameSettings.Kind.Parameter;
                result.ParameterName = settings.MenuSource.ParameterName;
            }
            return result;
        }

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
