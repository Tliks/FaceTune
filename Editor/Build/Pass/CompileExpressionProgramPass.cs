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
        
        var conditionCompiler = new ConditionCompiler(context.Root, platformSupport);
        var expressionCompiler = new ExpressionCompiler(context, settings, conditionCompiler);

        var items = components
            .Select(expressionCompiler.Compile)
            .ToList();

        return new ExpressionProgram(items);
    }
}

internal sealed class ExpressionCompiler
{
    private readonly AvatarContext _avatarContext;
    private readonly BuildSettings _settings;
    private readonly ConditionCompiler _conditionCompiler;
    private readonly IReadOnlyList<BlendShapeWeightAnimation> _safeZeroBlendShapeAnimations;

    public ExpressionCompiler(
        AvatarContext avatarContext,
        BuildSettings settings,
        ConditionCompiler conditionCompiler)
    {
        _avatarContext = avatarContext;
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
        var facialAnimationSet = CollectFacialAnimations(component);
        var expressionAnimationSet = CollectExpressionAnimations(component);

        return new ExpressionItem(
            component.transform,
            component.name,
            facialAnimationSet,
            CreateAnimationSet(component, facialAnimationSet, expressionAnimationSet),
            component.ExpressionSettings,
            ResolveFacialSettings(component),
            _conditionCompiler.Resolve(component));
    }

    private BlendShapeWeightAnimationSet CreateAnimationSet(
        FaceTuneComponent component,
        BlendShapeWeightAnimationSet facialAnimationSet,
        BlendShapeWeightAnimationSet expressionAnimationSet)
    {
        var animationSet = new BlendShapeWeightAnimationSet();

        if (component.FacialSettings.WriteMode == ExpressionWriteMode.Replace)
        {
            animationSet.AddRange(_safeZeroBlendShapeAnimations);
            animationSet.AddRange(facialAnimationSet);
        }

        animationSet.AddRange(expressionAnimationSet);
        return animationSet;
    }

    private BlendShapeWeightAnimationSet CollectFacialAnimations(FaceTuneComponent component)
    {
        var animationSet = new BlendShapeWeightAnimationSet();

        using var _ = ListPool<BlendShapeWeightAnimation>.Get(out var facialAnimations);
        if (FacialStyleContext.TryGetFacialStyleAnimations(component.gameObject, facialAnimations))
        {
            animationSet.AddRange(facialAnimations.Where(animation => !_settings.ExcludedBlendShapeNames.Contains(animation.Name)));
        }

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

    private static FacialSettings ResolveFacialSettings(FaceTuneComponent component)
    {
        var advancedEyeBlinkComponent = component.gameObject.GetComponentInParent<EyeBlinkComponent>(true);
        var blinkSettings = advancedEyeBlinkComponent == null
            ? AdvancedEyeBlinkSettings.Disabled()
            : advancedEyeBlinkComponent.ResolveSettings();

        var advancedLipSyncComponent = component.gameObject.GetComponentInParent<LipSyncComponent>(true);
        var lipSyncSettings = advancedLipSyncComponent == null
            ? AdvancedLipSyncSettings.Disabled()
            : advancedLipSyncComponent.ResolveSettings();

        return component.FacialSettings with
        {
            AdvancedEyBlinkSettings = blinkSettings,
            AdvancedLipSyncSettings = lipSyncSettings
        };
    }
}

internal sealed class ConditionCompiler
{
    private readonly GameObject _root;
    private readonly IMetabasePlatformSupport _platformSupport;

    public ConditionCompiler(GameObject root, IMetabasePlatformSupport platformSupport)
    {
        _root = root;
        _platformSupport = platformSupport;
    }

    public DnfCondition Resolve(FaceTuneComponent component)
    {
        var conditions = CollectEffectiveConditions(component).Select(ResolveCondition);
        var condition = DnfCondition.All(conditions);
        return ApplyConditionModifiers(component, condition);
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

        return DnfCondition.Any(condition.Cases.Select(ResolveConditionCase));
    }

    private DnfCondition ResolveConditionCase(ConditionCase conditionCase)
    {
        if (conditionCase.MenuConditions.Count != 0)
        {
            throw new InvalidOperationException("Menu conditions must be normalized before compiling expressions.");
        }

        var result = DnfCondition.Always;

        foreach (var handGestureCondition in conditionCase.HandGestureConditions)
        {
            result = result.And(_platformSupport.ResolveHandGestureCondition(handGestureCondition));
        }

        foreach (var parameterCondition in conditionCase.ParameterConditions)
        {
            result = result.And(_platformSupport.ResolveParameterCondition(parameterCondition));
        }

        return result;
    }

    private DnfCondition ApplyConditionModifiers(FaceTuneComponent component, DnfCondition condition)
    {
        var modifier = component.GetComponent<ExpressionConditionModifierComponent>();
        if (modifier == null) return condition;

        var originalGate = modifier.OriginalGate;
        var additionalActivation = modifier.AdditionalActivation;

        condition = originalGate != null ? condition.And(ResolveCondition(originalGate)) : condition;
        condition = additionalActivation != null ? condition.Or(ResolveCondition(additionalActivation)) : condition;

        return condition;
    }
}
