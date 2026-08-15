using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal class CompileExpressionProgramPass : FaceTunePass<CompileExpressionProgramPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.compile-expression-program";
    public override string DisplayName => "Compile Expression Program";

    protected override void Execute(FaceTuneContext context)
    {
        var settings = context.RequireSettings();
        context.SetExpressionProgram(FaceTuneProgramCompiler.Compile(
            context.AvatarContext,
            context.PlatformSupport,
            settings));
    }
}

internal static class FaceTuneProgramCompiler
{
    public static ExpressionProgram Compile(
        AvatarContext context,
        IMetabasePlatformSupport platformSupport,
        BuildSettings settings)
    {
        var components = context.Root.GetComponentsInChildren<ExpressionComponent>(true);
        
        var conditionCompiler = new ConditionCompiler(platformSupport, settings.ParameterDomains);
        var expressionCompiler = new ExpressionCompiler(context, platformSupport, settings, conditionCompiler);

        var items = components
            .Select(expressionCompiler.Compile)
            .Where(item => !item.RawWhen.IsNever)
            .ToList();

        return new ExpressionProgram(items);
    }
}

internal sealed class ExpressionCompiler
{
    private readonly AvatarContext _avatarContext;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly ConditionCompiler _conditionCompiler;
    private readonly FaceTuneResolver _resolver;
    private readonly IReadOnlyList<BlendShapeWeightAnimation> _safeZeroBlendShapeAnimations;

    public ExpressionCompiler(
        AvatarContext avatarContext,
        IMetabasePlatformSupport platformSupport,
        BuildSettings settings,
        ConditionCompiler conditionCompiler)
    {
        _avatarContext = avatarContext;
        _platformSupport = platformSupport;
        _conditionCompiler = conditionCompiler;
        _resolver = new FaceTuneResolver(avatarContext.Root);
        _safeZeroBlendShapeAnimations = avatarContext.FaceRenderer
            .GetBlendShapeWeights(avatarContext.FaceMesh)
            .Where(shape => !settings.ExcludedBlendShapeNames.Contains(shape.Name))
            .Select(shape => shape with { Weight = 0f })
            .ToBlendShapeAnimations()
            .ToArray();
    }

    public ExpressionItem Compile(ExpressionComponent component)
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

        return new ExpressionItem(
            component.transform,
            component.name,
            incomingAnimations,
            animations,
            component.WriteMode,
            ResolveMultiFrame(component.MultiFrame),
            component.AllowEyeBlink,
            component.AllowLipSync,
            _resolver.EyeBlink.Get(component),
            _resolver.LipSync.Get(component),
            _resolver.Transition.Get(component),
            _resolver.Priority.Get(component),
            _resolver.Conditions.Resolve(component, condition => _conditionCompiler.Resolve(condition) ?? DnfCondition.Never));
    }

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

internal sealed class ConditionCompiler
{
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly ParameterDomainRegistry _parameterDomains;

    public ConditionCompiler(
        IMetabasePlatformSupport platformSupport,
        ParameterDomainRegistry parameterDomains)
    {
        _platformSupport = platformSupport;
        _parameterDomains = parameterDomains;
    }

    private DnfCondition ResolveCondition(Condition condition)
    {
        var resolvedCases = condition.Cases
            .Select(ResolveConditionCase)
            .OfType<DnfCondition>()
            .ToList();

        return resolvedCases.Count == 0
            ? DnfCondition.Never
            : DnfCondition.Any(resolvedCases);
    }

    public DnfCondition? Resolve(Condition? condition)
        => condition == null ? null : ResolveCondition(condition);

    public DnfCondition? Resolve(object? condition)
    {
        if (condition == null) return null;

        return condition switch
        {
            HandGestureCondition handGesture =>
                _platformSupport.ResolveHandGestureCondition(handGesture, _parameterDomains),
            ParameterCondition parameter =>
                _platformSupport.ResolveParameterCondition(parameter, _parameterDomains),
            MenuCondition => throw new InvalidOperationException(
                "Menu conditions must be normalized before compiling expressions."),
            _ => throw new InvalidOperationException(
                $"Unsupported condition type: {condition?.GetType().FullName ?? "null"}")
        };
    }

    private DnfCondition? ResolveConditionCase(ConditionCase conditionCase)
    {
        var resolvedConditions = conditionCase.EnumerateConditions()
            .Select(Resolve)
            .OfType<DnfCondition>()
            .ToArray();
        return resolvedConditions.Length == 0
            ? null
            : DnfCondition.All(resolvedConditions);
    }
}
