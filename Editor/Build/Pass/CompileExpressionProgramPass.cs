using nadena.dev.modular_avatar.core;
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
        var autoMenuPlan = AutoMenuPlan.Create(context.Root, components, platformSupport);
        var conditionCompiler = new ConditionCompiler(context.Root, platformSupport, autoMenuPlan);
        var expressionCompiler = new ExpressionCompiler(context, settings, conditionCompiler);

        var items = components
            .Select(expressionCompiler.Compile)
            .ToList();

        return new ExpressionProgram(ResolvePriority(items));
    }

    private static IReadOnlyList<ExpressionItem> ResolvePriority(IReadOnlyList<ExpressionItem> items)
    {
        var result = items.ToList();
        var laterReplaceWhen = DnfCondition.Never;
        for (var i = result.Count - 1; i >= 0; i--)
        {
            var item = result[i];
            result[i] = item.WithSuppressedBy(laterReplaceWhen);

            if (item.WriteMode == ExpressionWriteMode.Replace)
            {
                laterReplaceWhen = laterReplaceWhen.Or(item.RawWhen);
            }
        }

        return result;
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
        return new ExpressionItem(
            component.transform,
            component.name,
            CollectAnimations(component),
            component.ExpressionSettings,
            ResolveFacialSettings(component),
            _conditionCompiler.Resolve(component));
    }

    private BlendShapeWeightAnimationSet CollectAnimations(FaceTuneComponent component)
    {
        var animationSet = new BlendShapeWeightAnimationSet();

        if (component.FacialSettings.WriteMode == ExpressionWriteMode.Replace)
        {
            AddReplaceDefaults(animationSet, component);
        }

        AddExpressionData(animationSet, component);
        return animationSet;
    }

    private void AddReplaceDefaults(BlendShapeWeightAnimationSet animationSet, FaceTuneComponent component)
    {
        animationSet.AddRange(_safeZeroBlendShapeAnimations);

        using var _ = ListPool<BlendShapeWeightAnimation>.Get(out var facialAnimations);
        if (FacialStyleContext.TryGetFacialStyleAnimations(component.gameObject, facialAnimations))
        {
            animationSet.AddRange(facialAnimations.Where(animation => !_settings.ExcludedBlendShapeNames.Contains(animation.Name)));
        }
    }


    private void AddExpressionData(BlendShapeWeightAnimationSet animationSet, FaceTuneComponent component)
    {
        ExpressionDataUtility.AddAnimations(component, animationSet, _avatarContext.BodyPath);

        var dataComponents = component.gameObject.GetComponentsInChildren<DataComponent>(true);
        foreach (var dataComponent in dataComponents)
        {
            ExpressionDataUtility.AddAnimations(dataComponent, animationSet, _avatarContext.BodyPath);
        }
    }

    private static FacialSettings ResolveFacialSettings(FaceTuneComponent component)
    {
        var advancedEyeBlinkComponent = component.gameObject.GetComponentInParent<EyeBlinkComponent>(true);
        var blinkSettings = advancedEyeBlinkComponent == null
            ? AdvancedEyeBlinkSettings.Disabled()
            : ComponentReferenceUtility.ResolveSettings(advancedEyeBlinkComponent);

        var advancedLipSyncComponent = component.gameObject.GetComponentInParent<LipSyncComponent>(true);
        var lipSyncSettings = advancedLipSyncComponent == null
            ? AdvancedLipSyncSettings.Disabled()
            : ComponentReferenceUtility.ResolveSettings(advancedLipSyncComponent);

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
    private readonly AutoMenuPlan _autoMenuPlan;

    public ConditionCompiler(GameObject root, IMetabasePlatformSupport platformSupport, AutoMenuPlan autoMenuPlan)
    {
        _root = root;
        _platformSupport = platformSupport;
        _autoMenuPlan = autoMenuPlan;
    }

    public DnfCondition Resolve(FaceTuneComponent component)
    {
        var conditions = CollectEffectiveConditions(component).Select(ResolveCondition);
        var condition = DnfCondition.All(conditions);
        return _autoMenuPlan.Apply(component.transform, condition);
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

        yield return component.Condition;
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
}

internal sealed class AutoMenuPlan
{
    private readonly IReadOnlyDictionary<Transform, AutoMenuEffect> _effects;

    private AutoMenuPlan(IReadOnlyDictionary<Transform, AutoMenuEffect> effects)
    {
        _effects = effects;
    }

    public static AutoMenuPlan Create(
        GameObject root,
        IReadOnlyList<FaceTuneComponent> components,
        IMetabasePlatformSupport platformSupport)
    {
        var autoMenus = root.GetComponentsInChildren<AutoMenuComponent>(true);
        if (autoMenus.Length == 0) return new AutoMenuPlan(new Dictionary<Transform, AutoMenuEffect>());

        if (autoMenus.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:AutoMenuPlan:MultipleAutoMenu", null, autoMenus);
        }

        return Create(autoMenus[0], components, platformSupport);
    }

    private static AutoMenuPlan Create(
        AutoMenuComponent autoMenu,
        IReadOnlyList<FaceTuneComponent> components,
        IMetabasePlatformSupport platformSupport)
    {
        var menuExpressions = ResolveMenuExpressions(autoMenu, components);
        var suppressedExpressions = ResolveSuppressedExpressions(autoMenu, components).ToHashSet();

        var selectionParameter = CreateAutoMenuParameterName(autoMenu.gameObject);
        var manualInactive = platformSupport.ResolveParameterCondition(ParameterCondition.Int(selectionParameter, ComparisonType.Equal, 0));

        var menuEffects = menuExpressions
            .Select((expression, index) =>
            {
                var selected = platformSupport.ResolveParameterCondition(ParameterCondition.Int(selectionParameter, ComparisonType.Equal, index + 1));
                return new
                {
                    Expression = expression,
                    Effect = suppressedExpressions.Contains(expression)
                        ? AutoMenuEffect.SelectedSuppressed(manualInactive, selected)
                        : AutoMenuEffect.SelectedAllowed(selected)
                };
            });
        var suppressedEffects = suppressedExpressions
            .Except(menuExpressions)
            .Select(expression => new
            {
                Expression = expression,
                Effect = AutoMenuEffect.Suppressed(manualInactive)
            });

        return new AutoMenuPlan(menuEffects
            .Concat(suppressedEffects)
            .ToDictionary(x => x.Expression, x => x.Effect));
    }

    private static Transform[] ResolveMenuExpressions(AutoMenuComponent autoMenu, IReadOnlyList<FaceTuneComponent> components)
    {
        var excludedExpressions = ResolveReferencedExpressions(autoMenu, autoMenu.ExcludeFromMenuTargets);
        return components
            .Select(component => component.transform)
            .Where(expression => !excludedExpressions.Contains(expression))
            .ToArray();
    }

    private static IEnumerable<Transform> ResolveSuppressedExpressions(AutoMenuComponent autoMenu, IReadOnlyList<FaceTuneComponent> components)
    {
        var allowedDuringManualLock = ResolveReferencedExpressions(autoMenu, autoMenu.AllowDuringManualLockTargets);
        return components
            .Select(component => component.transform)
            .Where(expression => !allowedDuringManualLock.Contains(expression))
            .ToArray();
    }

    private static HashSet<Transform> ResolveReferencedExpressions(
        AutoMenuComponent autoMenu,
        IEnumerable<AvatarObjectReference> references)
    {
        return references
            .Select(reference => reference.Get(autoMenu))
            .Where(target => target != null)
            .SelectMany(target => target.GetComponentsInChildren<FaceTuneComponent>(true))
            .Select(component => component.transform)
            .ToHashSet();
    }

    private static string CreateAutoMenuParameterName(GameObject source)
    {
        var baseName = source.name.Replace(" ", "_").Replace(".", "_");
        return $"{FaceTuneConstants.ParameterPrefix}/{baseName}/manual";
    }

    public DnfCondition Apply(Transform expression, DnfCondition condition)
    {
        return _effects.TryGetValue(expression, out var effect)
            ? effect.Apply(condition)
            : condition;
    }

    private sealed class AutoMenuEffect
    {
        private readonly DnfCondition? _originalGate;
        private readonly DnfCondition? _additionalActivation;

        private AutoMenuEffect(DnfCondition? originalGate, DnfCondition? additionalActivation)
        {
            _originalGate = originalGate;
            _additionalActivation = additionalActivation;
        }

        public static AutoMenuEffect SelectedSuppressed(DnfCondition manualInactive, DnfCondition selected)
        {
            return new AutoMenuEffect(manualInactive, selected);
        }

        public static AutoMenuEffect SelectedAllowed(DnfCondition selected)
        {
            return new AutoMenuEffect(null, selected);
        }

        public static AutoMenuEffect Suppressed(DnfCondition manualInactive)
        {
            return new AutoMenuEffect(manualInactive, null);
        }

        public DnfCondition Apply(DnfCondition condition)
        {
            var original = _originalGate == null ? condition : _originalGate.And(condition);
            return _additionalActivation == null ? original : original.Or(_additionalActivation);
        }
    }

}
