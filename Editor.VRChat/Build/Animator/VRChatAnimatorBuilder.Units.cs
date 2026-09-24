using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static partial class VRChatAnimatorBuilder
{
    private sealed record ExpressionUnit(
        int Id,
        int Priority,
        Transform Anchor,
        IReadOnlyList<ExpressionItem> Expressions);

    private static ExpressionUnit[] ResolveUnits(
        BuildSettings settings,
        ExpressionPlan expressionPlan,
        VirtualControllerContext controllerContext)
    {
        ISet<Transform> unitBoundaryTransforms;
        using (new Utils.ProfilingSampleScope("Build.Animator.FindUnitBoundaries"))
        {
            unitBoundaryTransforms = FindUnitBoundaryTransforms(
                settings,
                expressionPlan,
                controllerContext);
        }

        using (new Utils.ProfilingSampleScope("Build.Animator.ResolveUnits"))
        {
            var externalPartitions = FindExternalPartitions(
                expressionPlan,
                settings.AvatarContext,
                unitBoundaryTransforms);
            var unitGroups = expressionPlan.Items
                .GroupBy(item => (
                    Priority: item.Priority.Priority,
                    ExternalPartition: externalPartitions[item.SourceTransform]))
                .ToArray();
            var unitIds = unitGroups
                .OrderBy(group => group.Key.Priority)
                .ThenByDescending(group => group.Key.ExternalPartition)
                .Select((group, id) => (group.Key, Id: id))
                .ToDictionary(entry => entry.Key, entry => entry.Id);
            return unitGroups
                .Select(group => new ExpressionUnit(
                    unitIds[group.Key],
                    group.Key.Priority,
                    group.First().SourceTransform,
                    group.ToArray()))
                .ToArray();
        }
    }

    private static IReadOnlyDictionary<Transform, int> FindExternalPartitions(
        ExpressionPlan expressionPlan,
        AvatarContext avatarContext,
        ISet<Transform> unitBoundaryTransforms)
    {
        var expressionTransforms = expressionPlan.Items
            .Select(item => item.SourceTransform)
            .ToHashSet();
        var partitions = new Dictionary<Transform, int>();
        var partition = 0;
        var hasExpressionAbove = false;
        var hasBoundarySinceLastExpression = false;

        foreach (var transform in avatarContext.Root.GetComponentsInChildren<Transform>(true))
        {
            if (expressionTransforms.Contains(transform))
            {
                if (hasExpressionAbove && hasBoundarySinceLastExpression) partition++;
                partitions[transform] = partition;
                hasExpressionAbove = true;
                hasBoundarySinceLastExpression = false;
                continue;
            }

            if (hasExpressionAbove && !hasBoundarySinceLastExpression
                && unitBoundaryTransforms.Contains(transform))
            {
                hasBoundarySinceLastExpression = true;
            }
        }

        return partitions;
    }

    private static ISet<Transform> FindUnitBoundaryTransforms(
        BuildSettings settings,
        ExpressionPlan expressionPlan,
        VirtualControllerContext controllerContext)
    {
        var managedBindings = CollectManagedBindings(settings, expressionPlan).ToHashSet();
        if (managedBindings.Count == 0) return new HashSet<Transform>();

        return settings.AvatarContext.Root.GetComponentsInChildren<Transform>(true)
            .Where(transform => transform.TryGetComponent<ModularAvatarMergeAnimator>(out var merge)
                && merge.layerType == VRCAvatarDescriptor.AnimLayerType.FX
                && controllerContext.Controllers.TryGetValue(merge, out var controller)
                && CollectBindings(controller).Any(managedBindings.Contains))
            .ToHashSet();
    }

    private static IEnumerable<EditorCurveBinding> CollectManagedBindings(
        BuildSettings settings,
        ExpressionPlan expressionPlan)
    {
        foreach (var name in settings.AvatarContext.FaceMesh.GetBlendShapeNames()
                     .Where(name => !settings.IsBlendShapeExplicitlyExcluded(name)))
        {
            yield return EditorCurveBinding.FloatCurve(
                settings.AvatarContext.BodyPath,
                typeof(SkinnedMeshRenderer),
                FaceTuneConstants.BlendShapePropertyPrefix + name);
        }

        foreach (var item in expressionPlan.Items)
        {
            foreach (var (binding, _) in item.NonFacialAnimations.FloatCurves)
                yield return binding;
            foreach (var (binding, _) in item.NonFacialAnimations.ObjectCurves)
                yield return binding;
        }
    }

    private static IEnumerable<EditorCurveBinding> CollectBindings(
        VirtualAnimatorController controller)
        => controller.Layers
            .Where(layer => layer.StateMachine != null)
            .SelectMany(layer => layer.StateMachine!.AllStates())
            .SelectMany(state => CollectBindings(state.Motion))
            .Distinct();

    private static IEnumerable<EditorCurveBinding> CollectBindings(VirtualMotion? motion)
    {
        return motion switch
        {
            VirtualClip clip => clip.GetFloatCurveBindings().Concat(clip.GetObjectCurveBindings()),
            VirtualBlendTree tree => tree.Children
                .Where(child => child.Motion != null)
                .SelectMany(child => CollectBindings(child.Motion)),
            _ => Array.Empty<EditorCurveBinding>()
        };
    }
}
