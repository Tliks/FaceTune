namespace Aoyon.FaceTune.Build;

/// <summary>
/// Materializes authored blend shape outputs and removes blend shapes excluded from FaceTune control.
/// </summary>
internal class FilterExcludedBlendShapesPass : FaceTunePass<FilterExcludedBlendShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.filter-excluded-blend-shapes";
    public override string DisplayName => "Filter Excluded BlendShapes";

    protected override void Execute(FaceTuneContext context)
    {
        FilterBlendShapeOutputs(context.AvatarContext.BodyPath, context.AvatarContext.Root, context.RequireSettings());
    }

    private static void FilterBlendShapeOutputs(string bodyPath, GameObject root, BuildSettings settings)
    {
        foreach (var component in root.GetComponentsInChildren<FaceTuneComponent>(true))
        {
            foreach (var data in ExpressionDataUtility.ResolveData(component))
            {
                FilterBlendShapeAnimations(data, component, bodyPath, settings);
            }
        }

        foreach (var component in root.GetComponentsInChildren<DataComponent>(true))
        {
            foreach (var data in ExpressionDataUtility.ResolveData(component))
            {
                FilterBlendShapeAnimations(data, component, bodyPath, settings);
            }
        }

        foreach (var component in root.GetComponentsInChildren<StyleComponent>(true))
        {
            foreach (var data in ExpressionDataUtility.ResolveData(component))
            {
                FilterBlendShapeAnimations(data, component, string.Empty, settings);
            }
        }

        foreach (var component in root.GetComponentsInChildren<EyeBlinkComponent>(true))
        {
            FilterAdvancedEyeBlinkSettings(component, settings);
        }

        foreach (var component in root.GetComponentsInChildren<LipSyncComponent>(true))
        {
            FilterAdvancedLipSyncSettings(component, settings);
        }
    }

    private static void FilterBlendShapeAnimations(ExpressionData data, Component owner, string bodyPath, BuildSettings settings)
    {
        var animations = new List<BlendShapeWeightAnimation>();
        ExpressionDataUtility.AddAnimations(data, animations, bodyPath);

        data.BlendShapeAnimations = FilterBlendShapeAnimations(owner, animations, settings);
        data.Clip = null;
    }

    private static void FilterAdvancedEyeBlinkSettings(EyeBlinkComponent component, BuildSettings settings)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Direct) return;

        var advancedSettings = component.AdvancedEyeBlinkSettings;
        component.AdvancedEyeBlinkSettings = advancedSettings with
        {
            BlinkBlendShapeNames = FilterBlendShapeNames(component, advancedSettings.BlinkBlendShapeNames, settings),
            CancelerBlendShapeNames = FilterBlendShapeNames(component, advancedSettings.CancelerBlendShapeNames, settings)
        };
    }

    private static void FilterAdvancedLipSyncSettings(LipSyncComponent component, BuildSettings settings)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Direct) return;

        var advancedSettings = component.AdvancedLipSyncSettings;
        component.AdvancedLipSyncSettings = advancedSettings with
        {
            CancelerBlendShapeNames = FilterBlendShapeNames(component, advancedSettings.CancelerBlendShapeNames, settings)
        };
    }

    private static List<BlendShapeWeightAnimation> FilterBlendShapeAnimations(
        Component owner,
        IEnumerable<BlendShapeWeightAnimation> animations,
        BuildSettings settings)
    {
        var list = animations.ToList();
        WarnExcludedBlendShapes(owner, list.Select(animation => animation.Name), settings.ExcludedBlendShapeNames);
        return list
            .Where(animation => !settings.ExcludedBlendShapeNames.Contains(animation.Name))
            .ToList();
    }

    private static List<string> FilterBlendShapeNames(Component owner, IEnumerable<string> names, BuildSettings settings)
    {
        var list = names.ToList();
        WarnExcludedBlendShapes(owner, list, settings.ExcludedBlendShapeNames);
        return list
            .Where(name => !settings.ExcludedBlendShapeNames.Contains(name))
            .ToList();
    }

    private static void WarnExcludedBlendShapes(Component owner, IEnumerable<string> names, IReadOnlyCollection<string> excludedBlendShapeNames)
    {
        var removed = names
            .Where(excludedBlendShapeNames.Contains)
            .Distinct()
            .ToList();

        if (removed.Count == 0) return;

        LocalizedLog.Warning(
            "Log:warning:ProcessTrackedShapesPass:UnAllowedBlendShapesFound",
            $"{owner}:{string.Join(", ", removed)}");
    }
}
