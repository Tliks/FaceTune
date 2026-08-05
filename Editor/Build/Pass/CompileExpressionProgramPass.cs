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
        var components = context.Root.GetComponentsInChildren<FaceTuneComponent>(true);
        
        var conditionCompiler = new ConditionCompiler(context.Root, platformSupport, settings.ParameterDomains);
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
    private readonly BuildSettings _settings;
    private readonly ConditionCompiler _conditionCompiler;
    private readonly IReadOnlyList<BlendShapeWeightAnimation> _safeZeroBlendShapeAnimations;

    public ExpressionCompiler(
        AvatarContext avatarContext,
        IMetabasePlatformSupport platformSupport,
        BuildSettings settings,
        ConditionCompiler conditionCompiler)
    {
        _avatarContext = avatarContext;
        _platformSupport = platformSupport;
        _settings = settings;
        _conditionCompiler = conditionCompiler;
        _safeZeroBlendShapeAnimations = avatarContext.FaceRenderer
            .GetBlendShapeWeights(avatarContext.FaceMesh)
            .Where(shape => !settings.ExcludedBlendShapeNames.Contains(shape.Name))
            .Select(shape => shape with { Weight = 0f })
            .ToBlendShapeAnimations()
            .ToArray();
    }

    public ExpressionItem Compile(FaceTuneComponent component)
    {
        var facialAnimations = new List<BlendShapeWeightAnimation>();
        FacialStyleContext.TryGetFacialStyleAnimations(component.gameObject, facialAnimations, _avatarContext.BodyPath);

        var expressionAnimationSet = CollectExpressionAnimations(component);

        return new ExpressionItem(
            component.transform,
            component.name,
            new(facialAnimations),
            CreateAnimationSet(component, facialAnimations, expressionAnimationSet),
            ResolveExpressionSettings(component.ExpressionSettings),
            ResolveFacialSettings(component),
            ResolveTransitionDurationSeconds(component),
            _conditionCompiler.Resolve(component));
    }

    private ExpressionSettings ResolveExpressionSettings(ExpressionSettings settings)
    {
        if (settings.MultiFrameMode != MultiFrameMode.Trigger) return settings;
        var parameter = _platformSupport.ResolveGestureWeightParameter(settings.TriggerHand);
        return string.IsNullOrEmpty(parameter)
            ? settings with { MultiFrameMode = MultiFrameMode.Default }
            : settings with { MultiFrameMode = MultiFrameMode.Parameter, ParameterName = parameter };
    }

    private BlendShapeWeightAnimationSet CreateAnimationSet(
        FaceTuneComponent component,
        List<BlendShapeWeightAnimation> facialAnimations,
        BlendShapeWeightAnimationSet expressionAnimationSet)
    {
        var animationSet = new BlendShapeWeightAnimationSet();

        if (component.FacialSettings.WriteMode == ExpressionWriteMode.Replace)
        {
            animationSet.AddRange(_safeZeroBlendShapeAnimations);
            animationSet.AddRange(facialAnimations);
        }

        animationSet.AddRange(expressionAnimationSet);
        return animationSet;
    }

    private BlendShapeWeightAnimationSet CollectExpressionAnimations(FaceTuneComponent component)
    {
        var animationSet = new BlendShapeWeightAnimationSet();
        component.GetAnimations(animationSet, _avatarContext.BodyPath);

        var dataComponents = component.gameObject.GetComponentsInChildren<DataComponent>(true);
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetAnimations(animationSet, _avatarContext.BodyPath);
        }

        return animationSet;
    }

    private float ResolveTransitionDurationSeconds(FaceTuneComponent component)
    {
        return component.GetComponentInParent<TransitionComponent>(true)
            .DestroyedAsNull()?.DurationSeconds
            ?? TransitionComponent.DefaultDurationSeconds;
    }

    private static FacialSettings ResolveFacialSettings(FaceTuneComponent component)
    {
        var advancedEyeBlinkComponent = component.gameObject.GetComponentInParent<EyeBlinkComponent>(true);
        var blinkSettings = advancedEyeBlinkComponent == null
            ? new EyeBlinkSettings()
            : advancedEyeBlinkComponent.ResolveSettings();

        var advancedLipSyncComponent = component.gameObject.GetComponentInParent<LipSyncComponent>(true);
        var lipSyncSettings = advancedLipSyncComponent == null
            ? AdvancedLipSyncSettings.Disabled()
            : advancedLipSyncComponent.ResolveSettings();

        return component.FacialSettings with
        {
            EyeBlinkSettings = blinkSettings,
            AdvancedLipSyncSettings = lipSyncSettings
        };
    }
}

internal sealed class ConditionCompiler
{
    private readonly GameObject _root;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly ParameterDomainRegistry _parameterDomains;

    public ConditionCompiler(
        GameObject root,
        IMetabasePlatformSupport platformSupport,
        ParameterDomainRegistry parameterDomains)
    {
        _root = root;
        _platformSupport = platformSupport;
        _parameterDomains = parameterDomains;
    }

    public DnfCondition Resolve(FaceTuneComponent component)
    {
        var activationConditions = CollectEffectiveConditions(component)
            .Select(ResolveCondition)
            .ToArray();
        return activationConditions.Length == 0
            ? DnfCondition.Never
            : DnfCondition.All(activationConditions);
    }

    private IEnumerable<Condition> CollectEffectiveConditions(FaceTuneComponent component)
    {
        var current = component.transform;
        while (current != null)
        {
            foreach (var conditionComponent in current.GetComponents<ConditionComponent>())
            {
                yield return conditionComponent.Condition;
            }

            if (current.gameObject == _root) break;
            current = current.parent;
        }

        if (component.ConditionEnabled) yield return component.Condition;
    }

    private DnfCondition ResolveCondition(Condition condition)
    {
        if (condition.Always) return DnfCondition.Always;

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

    public DnfCondition? Resolve(ConditionBase? condition)
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
        var resolvedConditions = conditionCase.Conditions
            .Select(Resolve)
            .OfType<DnfCondition>()
            .ToArray();
        return resolvedConditions.Length == 0
            ? null
            : DnfCondition.All(resolvedConditions);
    }
}
