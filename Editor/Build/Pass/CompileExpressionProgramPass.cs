using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal class CompileExpressionProgramPass : FaceTunePass<CompileExpressionProgramPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.compile-expression-program";
    public override string DisplayName => "Compile Expression Program";

    protected override void Execute(FaceTuneContext context)
    {
        var settings = context.BuildContext.GetState(_ => FaceTuneBuildSettings.Default);
        context.BuildContext.GetState(_ => FaceTuneProgramCompiler.Compile(
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
        FaceTuneBuildSettings settings)
    {
        var conditionCompiler = new ConditionCompiler(context.Root, platformSupport);
        var expressionCompiler = new ExpressionCompiler(context, platformSupport, settings, conditionCompiler);

        var items = context.Root
            .GetComponentsInChildren<FaceTuneComponent>(true)
            .Select(expressionCompiler.Compile)
            .ToList();

        items = AutoMenuConditionRewriter.Rewrite(context.Root, platformSupport, items);

        ResolvePriority(items);
        return new ExpressionProgram(items);
    }

    private static void ResolvePriority(IReadOnlyList<ExpressionItem> items)
    {
        var laterReplaceWhen = DnfCondition.Never;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.Expression.WriteMode != ExpressionWriteMode.Replace)
            {
                item.SetSuppressedBy(DnfCondition.Never);
                continue;
            }

            item.SetSuppressedBy(laterReplaceWhen);
            laterReplaceWhen = laterReplaceWhen.Or(item.RawWhen);
        }
    }
}

internal sealed class ExpressionCompiler
{
    private readonly AvatarContext _avatarContext;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly FaceTuneBuildSettings _settings;
    private readonly ConditionCompiler _conditionCompiler;

    public ExpressionCompiler(
        AvatarContext avatarContext,
        IMetabasePlatformSupport platformSupport,
        FaceTuneBuildSettings settings,
        ConditionCompiler conditionCompiler)
    {
        _avatarContext = avatarContext;
        _platformSupport = platformSupport;
        _settings = settings;
        _conditionCompiler = conditionCompiler;
    }

    public ExpressionItem Compile(FaceTuneComponent component)
    {
        return new ExpressionItem(
            component.gameObject,
            ResolveExpression(component),
            _conditionCompiler.Resolve(component));
    }

    private AvatarExpression ResolveExpression(FaceTuneComponent component)
    {
        var animationSet = new BlendShapeWeightAnimationSet();

        if (component.FacialSettings.WriteMode == ExpressionWriteMode.Replace)
        {
            var safeZeroBlendShapes = _avatarContext.FaceRenderer
                .GetBlendShapeWeights(_avatarContext.FaceMesh)
                .Where(shape => !_settings.ExcludedBlendShapeNames.Contains(shape.Name))
                .Select(shape => shape with { Weight = 0f });
            animationSet.AddRange(safeZeroBlendShapes.ToBlendShapeAnimations());

            using var _ = ListPool<BlendShapeWeightAnimation>.Get(out var facialAnimations);
            if (FacialStyleContext.TryGetFacialStyleAnimations(component.gameObject, facialAnimations))
            {
                animationSet.AddRange(facialAnimations.Where(animation => !_settings.ExcludedBlendShapeNames.Contains(animation.Name)));
            }
        }

        ExpressionDataUtility.AddAnimations(component.Data, animationSet, _avatarContext.BodyPath);

        var dataComponents = component.gameObject.GetComponentsInChildren<DataComponent>(true);
        foreach (var dataComponent in dataComponents)
        {
            ExpressionDataUtility.AddAnimations(dataComponent.Data, animationSet, _avatarContext.BodyPath);
        }

        var advancedEyeBlinkComponent = component.gameObject.GetComponentInParent<EyeBlinkComponent>(true);
        var blinkSettings = advancedEyeBlinkComponent == null
            ? AdvancedEyeBlinkSettings.Disabled()
            : advancedEyeBlinkComponent.AdvancedEyeBlinkSettings;

        var advancedLipSyncComponent = component.gameObject.GetComponentInParent<LipSyncComponent>(true);
        var lipSyncSettings = advancedLipSyncComponent == null
            ? AdvancedLipSyncSettings.Disabled()
            : advancedLipSyncComponent.AdvancedLipSyncSettings;

        var facialSettings = component.FacialSettings with
        {
            AdvancedEyBlinkSettings = blinkSettings,
            AdvancedLipSyncSettings = lipSyncSettings
        };

        return new AvatarExpression(component.name, animationSet, component.ExpressionSettings, facialSettings);
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
        return DnfCondition.All(conditions);
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

internal static class AutoMenuConditionRewriter
{
    public static List<ExpressionItem> Rewrite(
        GameObject root,
        IMetabasePlatformSupport platformSupport,
        IReadOnlyList<ExpressionItem> items)
    {
        var result = items.ToList();
        foreach (var autoMenu in root.GetComponentsInChildren<AutoMenuComponent>(true))
        {
            result = Rewrite(autoMenu, platformSupport, result);
        }
        return result;
    }

    private static List<ExpressionItem> Rewrite(
        AutoMenuComponent autoMenu,
        IMetabasePlatformSupport platformSupport,
        IReadOnlyList<ExpressionItem> items)
    {
        var scopedItems = items
            .Where(item => item.SourceObject.transform.IsChildOf(autoMenu.transform))
            .ToList();
        if (scopedItems.Count == 0) return items.ToList();

        var excludedFromMenu = ResolveTargets(autoMenu, autoMenu.ExcludeFromMenuTargets);
        var allowDuringManualLock = ResolveTargets(autoMenu, autoMenu.AllowDuringManualLockTargets);
        var menuItems = scopedItems
            .Where(item => !ContainsAncestorOrSelf(excludedFromMenu, item.SourceObject))
            .ToList();

        var selectionParameter = CreateAutoMenuParameterName(autoMenu.gameObject);
        var none = platformSupport.ResolveParameterCondition(ParameterCondition.Int(selectionParameter, ComparisonType.Equal, 0));
        var manualActive = platformSupport.ResolveParameterCondition(ParameterCondition.Int(selectionParameter, ComparisonType.NotEqual, 0));
        var values = menuItems
            .Select((item, index) => (item.SourceObject, Value: index + 1))
            .ToDictionary(x => x.SourceObject, x => x.Value);

        return items.Select(RewriteItem).ToList();

        ExpressionItem RewriteItem(ExpressionItem item)
        {
            if (!scopedItems.Contains(item)) return item;

            var rawWhen = item.RawWhen;
            if (values.TryGetValue(item.SourceObject, out var selectedValue))
            {
                var selected = platformSupport.ResolveParameterCondition(ParameterCondition.Int(selectionParameter, ComparisonType.Equal, selectedValue));
                rawWhen = none.And(rawWhen).Or(selected);
            }
            else if (!ContainsAncestorOrSelf(allowDuringManualLock, item.SourceObject))
            {
                rawWhen = manualActive.Not().And(rawWhen);
            }

            return new ExpressionItem(item.SourceObject, item.Expression, rawWhen);
        }
    }

    private static HashSet<GameObject> ResolveTargets(AutoMenuComponent autoMenu, IEnumerable<nadena.dev.modular_avatar.core.AvatarObjectReference> references)
    {
        return references
            .Select(reference => reference.Get(autoMenu))
            .Where(target => target != null)
            .ToHashSet();
    }

    private static bool ContainsAncestorOrSelf(HashSet<GameObject> targets, GameObject source)
    {
        var current = source.transform;
        while (current != null)
        {
            if (targets.Contains(current.gameObject)) return true;
            current = current.parent;
        }
        return false;
    }

    private static string CreateAutoMenuParameterName(GameObject source)
    {
        var baseName = source.name.Replace(" ", "_").Replace(".", "_");
        return $"{FaceTuneConstants.ParameterPrefix}/{baseName}/manual";
    }
}
