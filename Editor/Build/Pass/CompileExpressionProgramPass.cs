using nadena.dev.ndmf;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal class CompileExpressionProgramPass : Pass<CompileExpressionProgramPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.compile-expression-program";
    public override string DisplayName => "Compile Expression Program";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;
        context.GetState(ctx => Compile(
            buildPassContext.AvatarContext,
            buildPassContext.PlatformSupport));
    }

    private static ExpressionProgram Compile(AvatarContext context, IMetabasePlatformSupport platformSupport)
    {
        var items = context.Root
            .GetComponentsInChildren<FaceTuneComponent>(true)
            .Select(component => new ExpressionItem(
                component.gameObject,
                ResolveExpression(component, context),
                ResolveRawWhen(component, context.Root, platformSupport)))
            .ToList();

        ResolvePriority(items);
        return new ExpressionProgram(items);
    }

    private static AvatarExpression ResolveExpression(FaceTuneComponent component, AvatarContext avatarContext)
    {
        var animationSet = new BlendShapeWeightAnimationSet();

        if (component.FacialSettings.WriteMode == ExpressionWriteMode.Replace)
        {
            animationSet.AddRange(avatarContext.SafeZeroBlendShapes.ToBlendShapeAnimations());

            using var _ = ListPool<BlendShapeWeightAnimation>.Get(out var facialAnimations);
            if (FacialStyleContext.TryGetFacialStyleAnimations(component.gameObject, facialAnimations))
            {
                animationSet.AddRange(facialAnimations);
            }
        }

        ExpressionDataUtility.ResolveAnimations(component.Data, animationSet, avatarContext);

        var dataComponents = component.gameObject.GetComponentsInChildren<DataComponent>(true);
        foreach (var dataComponent in dataComponents)
        {
            ExpressionDataUtility.ResolveAnimations(dataComponent, animationSet, avatarContext);
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

    private static DnfCondition ResolveRawWhen(
        FaceTuneComponent component,
        GameObject root,
        IMetabasePlatformSupport platformSupport)
    {
        var conditions = CollectEffectiveConditions(component, root)
            .Select(platformSupport.ResolveCondition);
        return DnfCondition.All(conditions);
    }

    private static IEnumerable<Condition> CollectEffectiveConditions(FaceTuneComponent component, GameObject root)
    {
        var current = component.transform;
        while (current != null)
        {
            foreach (var conditionComponent in current.GetComponents<ConditionComponent>())
            {
                yield return Clone(conditionComponent.Condition);
            }

            if (current.gameObject == root) break;
            current = current.parent;
        }

        yield return Clone(component.Condition);
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

    private static Condition Clone(Condition source)
    {
        return new Condition(source.Always, source.Cases.Select(Clone));
    }

    private static ConditionCase Clone(ConditionCase source)
    {
        var clone = new ConditionCase(
            source.HandGestureConditions.Select(Clone),
            source.ParameterConditions.Select(Clone));
        clone.MenuConditons.AddRange(source.MenuConditons.Select(Clone));
        return clone;
    }

    private static MenuConditon Clone(MenuConditon source)
    {
        return new MenuConditon { MenuSource = source.MenuSource };
    }

    private static HandGestureCondition Clone(HandGestureCondition source)
    {
        return new HandGestureCondition(source.HandGesture, source.Match);
    }

    private static ParameterCondition Clone(ParameterCondition source)
    {
        return new ParameterCondition(
            source.ParameterName,
            source.ParameterType,
            source.ComparisonType,
            source.FloatValue,
            source.IntValue,
            source.BoolValue);
    }
}
