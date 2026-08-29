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
    private readonly FaceTuneResolver _resolver;
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
        _resolver = new FaceTuneResolver(avatarContext.Root);
        _settings = settings;
    }

    public IEnumerable<ExpressionItem> Build(ExpressionComponent component)
    {
        var incomingFacialAnimations = new BlendShapeWeightAnimationSet();
        _resolver.FacialData.AddIncoming(
            component.transform,
            incomingFacialAnimations,
            _avatarContext.BodyPath);
        RemoveProhibitedAnimations(
            incomingFacialAnimations,
            FaceTuneWriteKind.FacialData);

        var localFacialAnimations = new BlendShapeWeightAnimationSet();
        _resolver.FacialData.AddLocal(
            component,
            localFacialAnimations,
            _avatarContext.BodyPath);
        _resolver.FacialData.AddLocalData(
            component.transform,
            localFacialAnimations,
            _avatarContext.BodyPath);
        RemoveProhibitedAnimations(
            localFacialAnimations,
            FaceTuneWriteKind.FacialData);

        var nonFacialAnimations = ResolveNonFacialAnimations(component);
        var eyeBlink = ResolveEyeBlink(component);
        var lipSync = ResolveLipSync(component);
        var transition = _resolver.Transition.Get(component);

        var priority = _resolver.Priority.Get(component);

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
        DnfCondition when)
        => new(
            component.transform,
            name,
            incomingFacialAnimations,
            localFacialAnimations,
            nonFacialAnimations,
            component.WriteMode,
            ResolveMultiFrame(component.MultiFrame),
            component.AllowEyeBlink,
            component.AllowLipSync,
            eyeBlink,
            lipSync,
            transition,
            priority,
            when);

    private EyeBlinkSettings ResolveEyeBlink(ExpressionComponent component)
    {
        var source = _resolver.EyeBlink.Get(component);
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

    private LipSyncSettings ResolveLipSync(ExpressionComponent component)
    {
        var source = _resolver.LipSync.Get(component);
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

    private ResolvedNonFacialAnimationSet ResolveNonFacialAnimations(ExpressionComponent component)
        => _resolver.NonFacialAnimations.Resolve(component, _avatarContext.BodyPath);

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
