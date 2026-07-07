using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal record AnimatorBuildPlan(IReadOnlyList<OutputUnit> Units, RuntimeDomainRegistry RuntimeRegistry)
{
    public static AnimatorBuildPlan From(
        ExpressionProgram program,
        BuildSettings settings,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext)
    {
        var avatarContext = settings.AvatarContext;

        var managedBlendShapeNames = avatarContext.FaceMesh.GetBlendShapeNames()
            .Where(name => !settings.ExcludedBlendShapeNames.Contains(name))
            .ToHashSet();

        var splitIndices = FindExternalOverlapSplitIndices(
            program.Items,
            avatarContext.Root.transform,
            platformServices,
            controllerContext,
            managedBlendShapeNames);

        var units = new List<OutputUnit>();
        var start = 0;
        foreach (var splitIndex in splitIndices.Append(program.Items.Count))
        {
            var expressions = program.Items.Skip(start).Take(splitIndex - start).ToArray();
            var items = expressions.Select(expression => new AnimatorExpressionItem(
                expression,
                ResolveRuntimeModes(expression.FacialSettings))).ToArray();
            units.Add(new OutputUnit(units.Count, expressions[0].SourceTransform, items));
            start = splitIndex;
        }

        var registry = RuntimeDomainRegistry.Create(units);
        return new AnimatorBuildPlan(units, registry);
    }

    private static ExpressionRuntimeModes ResolveRuntimeModes(FacialSettings settings)
    {
        return new ExpressionRuntimeModes(
            ResolveEyeBlinkMode(settings),
            ResolveLipSyncMode(settings));
    }

    private static EyeBlinkRuntimeMode? ResolveEyeBlinkMode(FacialSettings settings)
    {
        if (settings.AdvancedEyBlinkSettings.IsAnimationEnabled())
        {
            return EyeBlinkRuntimeMode.Advanced(settings.AdvancedEyBlinkSettings);
        }

        return settings.AllowEyeBlink switch
        {
            TrackingPermission.Allow => EyeBlinkRuntimeMode.Tracking,
            TrackingPermission.Disallow => EyeBlinkRuntimeMode.Disabled,
            _ => null
        };
    }

    private static LipSyncRuntimeMode? ResolveLipSyncMode(FacialSettings settings)
    {
        if (settings.AdvancedLipSyncSettings.IsCancelerEnabled())
        {
            return LipSyncRuntimeMode.Canceler(settings.AdvancedLipSyncSettings);
        }

        return settings.AllowLipSync switch
        {
            TrackingPermission.Allow => LipSyncRuntimeMode.Tracking,
            TrackingPermission.Disallow => LipSyncRuntimeMode.Disabled,
            _ => null
        };
    }

    private static IEnumerable<int> FindExternalOverlapSplitIndices(
        IReadOnlyList<ExpressionItem> items,
        Transform root,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext,
        ISet<string> managedBlendShapeNames)
    {
        if (items.Count < 2 || managedBlendShapeNames.Count == 0) yield break;

        var expressionIndexByTransform = items
            .Select((item, index) => (item.SourceTransform, index))
            .ToDictionary(entry => entry.SourceTransform, entry => entry.index);

        var hasExpressionAbove = false;
        var hasBoundarySinceLastExpression = false;

        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (expressionIndexByTransform.TryGetValue(transform, out var expressionIndex))
            {
                if (hasExpressionAbove && hasBoundarySinceLastExpression)
                {
                    yield return expressionIndex;
                }

                hasExpressionAbove = true;
                hasBoundarySinceLastExpression = false;
                continue;
            }

            if (!hasExpressionAbove || hasBoundarySinceLastExpression) continue;

            hasBoundarySinceLastExpression = platformServices.IsUnitBoundaryTransform(
                transform,
                controllerContext,
                managedBlendShapeNames);
        }
    }
}

internal sealed record class AnimatorExpressionItem(
    ExpressionItem Expression,
    ExpressionRuntimeModes RuntimeModes);

internal sealed class OutputUnit
{
    public int Id { get; }
    public Transform Anchor { get; }
    public IReadOnlyList<AnimatorExpressionItem> Items { get; }
    public IReadOnlyList<PackedLayer> Layers { get; }

    public OutputUnit(int id, Transform anchor, IReadOnlyList<AnimatorExpressionItem> items)
    {
        Id = id;
        Anchor = anchor;
        Items = items;
        Layers = PackLayers(items);
    }

    private static IReadOnlyList<PackedLayer> PackLayers(IReadOnlyList<AnimatorExpressionItem> items)
    {
        var layers = new List<PackedLayer>();
        for (var index = 0; index < items.Count;)
        {
            var item = items[index];
            if (item.Expression.WriteMode == ExpressionWriteMode.Replace)
            {
                var run = new List<AnimatorExpressionItem>();
                while (index < items.Count && items[index].Expression.WriteMode == ExpressionWriteMode.Replace)
                {
                    run.Add(items[index]);
                    index++;
                }
                layers.Add(new PackedLayer(PackedLayerKind.ReplaceRun, run));
                continue;
            }

            // Blend remains one layer per expression until simple exclusivity proof is added.
            layers.Add(new PackedLayer(PackedLayerKind.Blend, new[] { item }));
            index++;
        }

        return layers;
    }
}

internal sealed record class PackedLayer(PackedLayerKind Kind, IReadOnlyList<AnimatorExpressionItem> Items)
{
    public string Name => Kind == PackedLayerKind.ReplaceRun && Items.Count > 1
        ? $"ReplaceRun {Items[0].Expression.Name}..{Items[^1].Expression.Name}"
        : Items[0].Expression.Name;

    public DnfCondition StateWhen(int index)
    {
        var expression = Items[index].Expression;
        if (Kind != PackedLayerKind.ReplaceRun) return expression.RawWhen;

        var higherReplaceWhen = DnfCondition.Any(Items
            .Skip(index + 1)
            .Select(item => item.Expression.RawWhen));
        return expression.RawWhen.Except(higherReplaceWhen);
    }
}

internal enum PackedLayerKind
{
    ReplaceRun,
    Blend
}
