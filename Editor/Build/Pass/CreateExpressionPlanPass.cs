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
    private readonly FacialAnimationResolver _facial;
    private readonly NonFacialAnimationResolver _nonFacial;
    private readonly ExpressionBehaviorResolver _behavior;
    private readonly MultiFrameResolver _multiFrame;
    private readonly EyeBlinkResolver _eyeBlink;
    private readonly LipSyncResolver _lipSync;
    private readonly TransitionResolver _transition;
    private readonly PriorityResolver _priority;
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
        _facial = new FacialAnimationResolver(avatarContext.Root);
        _nonFacial = new NonFacialAnimationResolver(avatarContext.Root);
        _behavior = new ExpressionBehaviorResolver();
        _multiFrame = new MultiFrameResolver();
        _eyeBlink = new EyeBlinkResolver(avatarContext.Root);
        _lipSync = new LipSyncResolver(avatarContext.Root);
        _transition = new TransitionResolver(avatarContext.Root);
        _priority = new PriorityResolver(avatarContext.Root);
        _settings = settings;
    }

    public IEnumerable<ExpressionItem> Build(ExpressionComponent component)
    {
        var incomingFacialAnimations = _facial.ResolveIncoming(component.transform, _avatarContext.BodyPath);
        RemoveProhibited(incomingFacialAnimations, FaceTuneWriteKind.FacialData);

        var localFacialAnimations = _facial.TryResolve(component, _avatarContext.BodyPath, out var resolvedFacial)
            ? resolvedFacial
            : new BlendShapeWeightAnimationSet();
        RemoveProhibited(localFacialAnimations, FaceTuneWriteKind.FacialData);

        var nonFacialAnimations = _nonFacial.Resolve(component, _avatarContext.BodyPath);
        var eyeBlink = _eyeBlink.Resolve(component);
        var lipSync = _lipSync.Resolve(component);
        RemoveProhibited(eyeBlink);
        RemoveProhibited(lipSync);
        var transition = _transition.Resolve(component);
        var priority = _priority.Resolve(component);
        var behavior = _behavior.Resolve(component);
        var multiFrame = _multiFrame.Resolve(component);

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
            behavior,
            multiFrame,
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
            behavior,
            multiFrame,
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
        ExpressionBehavior behavior,
        MultiFrameSettings multiFrame,
        DnfCondition when)
        => new(
            component.transform,
            name,
            incomingFacialAnimations,
            localFacialAnimations,
            nonFacialAnimations,
            behavior.WriteMode,
            ResolveMultiFrame(multiFrame),
            behavior.AllowEyeBlink,
            behavior.AllowLipSync,
            eyeBlink,
            lipSync,
            transition,
            priority,
            when);

    private bool CanWrite(FaceTuneWriteKind writeKind, string name)
        => _settings.CanWriteBlendShape(writeKind, name);

    private void RemoveProhibited<T>(List<T> items, FaceTuneWriteKind writeKind, Func<T, string> nameOf)
        => items.RemoveAll(item => !CanWrite(writeKind, nameOf(item)));

    private void RemoveProhibited(BlendShapeWeightAnimationSet animations, FaceTuneWriteKind writeKind)
    {
        var prohibited = animations
            .Where(animation => !CanWrite(writeKind, animation.Name))
            .Select(animation => animation.Name)
            .ToArray();
        animations.RemoveRange(prohibited);
    }

    private void RemoveProhibited(EyeBlinkSettings settings)
    {
        RemoveProhibited(
            settings.SimpleBlinkBlendShapes,
            FaceTuneWriteKind.EyeBlinkAnimation,
            static shape => shape.Name);
        RemoveProhibited(
            settings.SimpleConflictPreventionBlendShapes,
            FaceTuneWriteKind.FacialData,
            static shape => shape.Name);
        RemoveProhibited(
            settings.Animations,
            FaceTuneWriteKind.EyeBlinkAnimation,
            static animation => animation.Name);
    }

    private void RemoveProhibited(LipSyncSettings settings)
        => RemoveProhibited(
            settings.CancellerBlendShapes,
            FaceTuneWriteKind.FacialData,
            static shape => shape.Name);

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
            else if (settings.MenuSource.MenuKind != MenuComponent.Kind.Radial)
            {
                // Motion Time はパラメータへ書き込むため、Float の Radial 以外では選択値を破壊する。
                Debug.LogWarning(
                    $"Multi frame menu source '{settings.MenuSource.name}' must be a radial menu. Falling back to default.",
                    settings.MenuSource);
                result.MultiFrameMode = MultiFrameSettings.Kind.Default;
            }
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
