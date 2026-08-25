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
        
        var conditionResolver = new ConditionResolver(platformSupport, settings.ParameterDomains);
        var expressionBuilder = new ExpressionItemBuilder(context, platformSupport, settings, conditionResolver);

        var items = components
            .SelectMany(expressionBuilder.Build)
            .Where(item => !item.RawWhen.IsNever)
            .ToList();
        expressionBuilder.ValidateBlendShapeUsage();

        return new ExpressionPlan(items);
    }
}

internal sealed class ExpressionItemBuilder
{
    private readonly AvatarContext _avatarContext;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly ConditionResolver _conditionResolver;
    private readonly FaceTuneResolver _resolver;
    private readonly ImmutableHashSet<string> _explicitlyExcluded;
    private readonly ISet<string> _availableBlendShapeNames;
    private readonly IReadOnlyList<BlendShapeWeightAnimation> _safeZeroBlendShapeAnimations;
    private readonly Dictionary<string, Component?> _eyeBlinkOwners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Component?> _lipSyncOwners = new(StringComparer.Ordinal);

    public ExpressionItemBuilder(
        AvatarContext avatarContext,
        IMetabasePlatformSupport platformSupport,
        BuildSettings settings,
        ConditionResolver conditionResolver)
    {
        _avatarContext = avatarContext;
        _platformSupport = platformSupport;
        _conditionResolver = conditionResolver;
        _resolver = new FaceTuneResolver(avatarContext.Root);
        _explicitlyExcluded = settings.ExplicitlyExcludedBlendShapeNames;
        _availableBlendShapeNames = avatarContext.FaceMesh.GetBlendShapeNames().ToHashSet(StringComparer.Ordinal);
        AddExternalNames(settings.ExternalEyeBlinkBlendShapeNames, _eyeBlinkOwners);
        AddExternalNames(settings.ExternalLipSyncBlendShapeNames, _lipSyncOwners);
        _safeZeroBlendShapeAnimations = settings.GetManagedZeroBlendShapes()
            .ToBlendShapeAnimations()
            .ToArray();
    }

    public IEnumerable<ExpressionItem> Build(ExpressionComponent component)
    {
        var incomingAnimations = new BlendShapeWeightAnimationSet();
        _resolver.FacialData.AddIncoming(component, incomingAnimations, _avatarContext.BodyPath);
        incomingAnimations.RemoveRange(_explicitlyExcluded);
        var localAnimations = new BlendShapeWeightAnimationSet();
        _resolver.FacialData.AddLocal(component, localAnimations, _avatarContext.BodyPath);
        localAnimations.RemoveRange(_explicitlyExcluded);
        var animations = new BlendShapeWeightAnimationSet();
        if (component.WriteMode == ExpressionWriteMode.Replace)
        {
            animations.AddRange(_safeZeroBlendShapeAnimations);
            animations.AddRange(incomingAnimations);
        }
        animations.AddRange(localAnimations);
        var nonFacialAnimations = ResolveNonFacialAnimations(component);
        var eyeBlink = ResolveEyeBlink(component);
        var lipSync = ResolveLipSync(component);
        var transition = _resolver.Transition.Get(component);

        var priority = _resolver.Priority.Get(component);

        yield return BuildItem(
            component,
            component.name,
            incomingAnimations,
            animations,
            nonFacialAnimations,
            eyeBlink,
            lipSync,
            transition,
            priority,
            _conditionResolver.Resolve(component));

        var directCondition = component.DirectMenuSettings.GeneratedCondition;
        if (!component.DirectMenuEnabled || directCondition == null) yield break;

        yield return BuildItem(
            component,
            $"{component.name} (Direct Menu)",
            incomingAnimations,
            animations,
            nonFacialAnimations,
            eyeBlink,
            lipSync,
            transition,
            new PrioritySettings
            {
                Priority = priority.Priority + component.DirectMenuSettings.PriorityOffset
            },
            _conditionResolver.Resolve(directCondition) ?? DnfCondition.Never);
    }

    private ExpressionItem BuildItem(
        ExpressionComponent component,
        string name,
        BlendShapeWeightAnimationSet incomingAnimations,
        BlendShapeWeightAnimationSet animations,
        ResolvedNonFacialAnimationSet nonFacialAnimations,
        EyeBlinkSettings eyeBlink,
        LipSyncSettings lipSync,
        TransitionSettings transition,
        PrioritySettings priority,
        DnfCondition when)
        => new(
            component.transform,
            name,
            incomingAnimations,
            animations,
            nonFacialAnimations,
            component.WriteMode,
            ResolveMultiFrame(component.MultiFrame),
            component.AllowEyeBlink,
            component.AllowLipSync,
            eyeBlink,
            lipSync,
            transition,
            priority,
            when);

    public void ValidateBlendShapeUsage()
    {
        var conflicts = _eyeBlinkOwners.Keys
            .Intersect(_lipSyncOwners.Keys, StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (conflicts.Length == 0) return;

        var details = conflicts.Select(name =>
        {
            var eyeOwner = DescribeOwner(_eyeBlinkOwners[name]);
            var lipOwner = DescribeOwner(_lipSyncOwners[name]);
            return $"'{name}' (EyeBlink: {eyeOwner}, LipSync: {lipOwner})";
        });
        throw new InvalidOperationException(
            "BlendShapes cannot be controlled by both EyeBlink and LipSync: "
            + string.Join(", ", details));
    }

    private EyeBlinkSettings ResolveEyeBlink(ExpressionComponent component)
    {
        var source = _resolver.EyeBlink.Get(component, out var owner);
        var result = new EyeBlinkSettings
        {
            EyeBlinkMode = source.EyeBlinkMode,
            IntervalSeconds = source.IntervalSeconds,
            SimpleDurationsSeconds = source.SimpleDurationsSeconds,
            SimpleBlinkBlendShapes = source.SimpleBlinkBlendShapes
                .Where(shape => !IsExplicitlyExcluded(shape.Name))
                .ToList(),
            SimpleConflictPreventionBlendShapes = source.SimpleConflictPreventionBlendShapes
                .Where(shape => !IsExplicitlyExcluded(shape.Name))
                .ToList(),
            Animations = source.Animations
                .Where(animation => !IsExplicitlyExcluded(animation.Name))
                .ToList()
        };

        IEnumerable<string> names = result.EyeBlinkMode switch
        {
            EyeBlinkSettings.Kind.BuiltIn => Array.Empty<string>(),
            EyeBlinkSettings.Kind.SimpleAnimation => result.SimpleBlinkBlendShapes
                .Select(shape => shape.Name),
            EyeBlinkSettings.Kind.CustomAnimation => result.Animations
                .Select(animation => animation.Name),
            _ => throw new ArgumentOutOfRangeException()
        };
        foreach (var name in names)
        {
            if (!_availableBlendShapeNames.Contains(name)) continue;
            if (owner != null || !_eyeBlinkOwners.ContainsKey(name))
                _eyeBlinkOwners[name] = owner;
        }
        return result;
    }

    private LipSyncSettings ResolveLipSync(ExpressionComponent component)
    {
        var source = _resolver.LipSync.Get(component);
        return new LipSyncSettings
        {
            CancellerBlendShapes = source.CancellerBlendShapes
                .Where(shape => !IsExplicitlyExcluded(shape.Name))
                .ToList()
        };
    }

    private void AddExternalNames(
        IEnumerable<string> names,
        IDictionary<string, Component?> owners)
    {
        foreach (var name in names)
        {
            if (!IsExplicitlyExcluded(name) && _availableBlendShapeNames.Contains(name))
                owners.TryAdd(name, (Component?)null);
        }
    }

    private bool IsExplicitlyExcluded(string name)
        => _explicitlyExcluded.Contains(name);

    private static string DescribeOwner(Component? owner)
        => owner == null ? "external platform control" : owner.ToString();

    private ResolvedNonFacialAnimationSet ResolveNonFacialAnimations(ExpressionComponent component)
    {
        var result = new ResolvedNonFacialAnimationSet();
        foreach (var (owner, data) in _resolver.ExpressionData
                     .EnumerateLocal<NonFacialAnimationData>(component))
            AddNonFacialAnimations(result, owner, data);
        return result;
    }

    private void AddNonFacialAnimations(
        ResolvedNonFacialAnimationSet result,
        Component owner,
        NonFacialAnimationData data)
    {
        foreach (var clip in data.AnimationClips.Where(clip => clip != null))
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (IsFacialBlendShapeBinding(binding)) continue;
                result.AddFloatCurve(binding, AnimationUtility.GetEditorCurve(clip, binding));
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                result.AddObjectCurve(
                    binding,
                    AnimationUtility.GetObjectReferenceCurve(clip, binding));
            }
        }

        foreach (var animation in data.TransformAnimations)
        {
            var target = animation?.Target.Get(owner);
            if (target == null) continue;
            var path = Utils.GetRelativePath(_avatarContext.Root.gameObject, target);
            if (path == null) continue;
            result.AddFloatCurve(
                EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"),
                animation.Curve ?? AnimationCurve.Constant(0f, 1f, 1f));
        }
    }

    private bool IsFacialBlendShapeBinding(EditorCurveBinding binding)
        => binding.path == _avatarContext.BodyPath
        && binding.type == typeof(SkinnedMeshRenderer)
        && binding.propertyName.StartsWith(FaceTuneConstants.BlendShapePropertyPrefix, StringComparison.Ordinal);

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
