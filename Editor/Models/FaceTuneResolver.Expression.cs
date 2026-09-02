using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal sealed class ExpressionDefinitionResolver
{
    private readonly ComputeContext context;

    public ExpressionDefinitionResolver(ComputeContext? context = null)
    {
        this.context = context ?? ComputeContext.NullContext;
    }

    public IExpressionDefinitionProvider? Resolve(ExpressionComponent expression)
        => Resolve(expression, new HashSet<Component>());

    private IExpressionDefinitionProvider? Resolve(
        IExpressionDefinitionProvider provider,
        HashSet<Component> path)
    {
        var component = (Component)provider;
        if (!path.Add(component)) return null;
        try
        {
            if (provider is not IExpressionDefinitionProviderWithReference) return provider;
            var reference = context.Observe(
                component,
                current =>
                {
                    var value = (IExpressionDefinitionProviderWithReference)current;
                    return (Mode: value.DefinitionMode, Source: value.DefinitionSource);
                },
                (left, right) => left == right);
            if (reference.Mode == SettingsReferenceMode.Direct) return provider;
            return reference.Source == null
                ? null
                : context.GetComponents<FaceTuneTagComponent>(reference.Source.gameObject)
                    .OfType<IExpressionDefinitionProvider>()
                    .Select(candidate => Resolve(candidate, path))
                    .LastOrDefault(candidate => candidate != null);
        }
        finally
        {
            path.Remove(component);
        }
    }
}

internal sealed class ExpressionBehaviorResolver
{
    private readonly ExpressionDefinitionResolver definitions;
    private readonly SettingValueResolver<ExpressionBehavior> values;

    public ExpressionBehaviorResolver(ComputeContext? context = null)
    {
        definitions = new ExpressionDefinitionResolver(context);
        values = new SettingValueResolver<ExpressionBehavior>(static (_, value) => value, context);
    }

    public ExpressionBehavior Resolve(ExpressionComponent expression)
        => definitions.Resolve(expression) is ISettingProvider<ExpressionBehavior> provider
            ? values.Resolve(provider) ?? ExpressionBehavior.Default
            : ExpressionBehavior.Default;
}

internal sealed class MultiFrameResolver
{
    private readonly ExpressionDefinitionResolver definitions;
    private readonly SettingValueResolver<MultiFrameSettings> values;

    public MultiFrameResolver(ComputeContext? context = null)
    {
        definitions = new ExpressionDefinitionResolver(context);
        values = new SettingValueResolver<MultiFrameSettings>(static (_, value) => value.Clone(), context);
    }

    public MultiFrameSettings Resolve(ExpressionComponent expression)
        => definitions.Resolve(expression) is ISettingProvider<MultiFrameSettings> provider
            ? values.Resolve(provider) ?? new MultiFrameSettings()
            : new MultiFrameSettings();
}

internal sealed class EyeBlinkResolver
{
    private readonly ExpressionDefinitionResolver definitions;
    private readonly SettingValueResolver<EyeBlinkSettings> references;
    private readonly ScopedValueResolver<EyeBlinkSettings> scope;

    public EyeBlinkResolver(GameObject root, ComputeContext? context = null)
    {
        definitions = new ExpressionDefinitionResolver(context);
        references = new SettingValueResolver<EyeBlinkSettings>(static (_, value) => value.Clone(), context);
        scope = new ScopedValueResolver<EyeBlinkSettings>(
            root,
            settings => references.Resolve(settings),
            static () => new EyeBlinkSettings(),
            context);
    }

    public EyeBlinkSettings? ResolveDefinition(ExpressionComponent expression)
        => definitions.Resolve(expression) is ISettingProvider<EyeBlinkSettings> provider
            ? references.Resolve(provider)
            : null;

    public ScopedValue<EyeBlinkSettings> ResolveInherited(ExpressionComponent expression)
        => scope.GetIncoming(expression);

    public EyeBlinkSettings Resolve(ExpressionComponent expression)
        => ResolveDefinition(expression) ?? ResolveInherited(expression).Value;
}

internal sealed class LipSyncResolver
{
    private readonly ExpressionDefinitionResolver definitions;
    private readonly SettingValueResolver<LipSyncSettings> references;
    private readonly ScopedValueResolver<LipSyncSettings> scope;

    public LipSyncResolver(GameObject root, ComputeContext? context = null)
    {
        definitions = new ExpressionDefinitionResolver(context);
        references = new SettingValueResolver<LipSyncSettings>(static (_, value) => value.Clone(), context);
        scope = new ScopedValueResolver<LipSyncSettings>(
            root,
            settings => references.Resolve(settings),
            static () => new LipSyncSettings(),
            context);
    }

    public LipSyncSettings? ResolveDefinition(ExpressionComponent expression)
        => definitions.Resolve(expression) is ISettingProvider<LipSyncSettings> provider
            ? references.Resolve(provider)
            : null;

    public ScopedValue<LipSyncSettings> ResolveInherited(ExpressionComponent expression)
        => scope.GetIncoming(expression);

    public LipSyncSettings Resolve(ExpressionComponent expression)
        => ResolveDefinition(expression) ?? ResolveInherited(expression).Value;
}

internal sealed class TransitionResolver
{
    private readonly SettingValueResolver<TransitionSettings> values;
    private readonly ScopedValueResolver<TransitionSettings> scope;

    public TransitionResolver(GameObject root, ComputeContext? context = null)
    {
        values = new SettingValueResolver<TransitionSettings>(
            static (_, value) => new TransitionSettings { DurationSeconds = value.DurationSeconds },
            context);
        scope = new ScopedValueResolver<TransitionSettings>(
            root,
            settings => values.Resolve(settings),
            static () => new TransitionSettings(),
            context);
    }

    public ScopedValue<TransitionSettings> ResolveInherited(ExpressionComponent expression)
        => scope.GetIncoming(expression);

    public TransitionSettings Resolve(ExpressionComponent expression)
        => values.Resolve(expression) ?? ResolveInherited(expression).Value;
}

internal sealed class PriorityResolver
{
    private readonly SettingValueResolver<PrioritySettings> values;
    private readonly ScopedValueResolver<PrioritySettings> scope;

    public PriorityResolver(GameObject root, ComputeContext? context = null)
    {
        values = new SettingValueResolver<PrioritySettings>(
            static (_, value) => new PrioritySettings { Priority = value.Priority },
            context);
        scope = new ScopedValueResolver<PrioritySettings>(
            root,
            settings => values.Resolve(settings),
            static () => new PrioritySettings(),
            context);
    }

    public ScopedValue<PrioritySettings> ResolveInherited(ExpressionComponent expression)
        => scope.GetIncoming(expression);

    public PrioritySettings Resolve(ExpressionComponent expression)
        => values.Resolve(expression) ?? ResolveInherited(expression).Value;
}


internal sealed class FacialAnimationResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;
    private readonly ExpressionDefinitionResolver _definitions;

    public FacialAnimationResolver(GameObject root, ComputeContext? context = null)
    {
        _root = root;
        _context = context ?? ComputeContext.NullContext;
        _definitions = new ExpressionDefinitionResolver(context);
    }

    public bool TryResolve(
        Component component,
        string bodyPath,
        [NotNullWhen(true)] out BlendShapeWeightAnimationSet? value)
    {
        value = Resolve(component, bodyPath, new HashSet<Component>());
        return value != null;
    }

    public bool TryResolveBase(
        Component component,
        string bodyPath,
        [NotNullWhen(true)] out BlendShapeWeightAnimationSet? value)
    {
        var data = ReadData(component);
        if (data == null)
        {
            value = null;
            return false;
        }
        value = ResolveData(data, bodyPath, new HashSet<Component>(), includeLocal: false);
        return true;
    }

    public BlendShapeWeightAnimationSet ResolveIncoming(Transform target, string bodyPath)
    {
        var result = new BlendShapeWeightAnimationSet();
        foreach (var settings in _context.GetComponentsInParentExcludingSelf<SettingsComponent>(
                     _root,
                     target,
                     true))
        {
            if (Resolve(settings, bodyPath, new HashSet<Component>()) is { } value)
                result.AddRange(value);
        }
        return result;
    }

    public void AddRenderer(ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        foreach (var settings in _context.GetComponentsInChildren<SettingsComponent>(_root, true))
        {
            var enabled = _context.Observe(
                settings,
                value => value.ApplyToRenderer,
                (left, right) => left == right);
            if (!enabled || Resolve(settings, bodyPath, new HashSet<Component>()) is not { } value)
                continue;
            foreach (var animation in value) result.Add(animation);
        }
    }

    private BlendShapeWeightAnimationSet? Resolve(
        Component? component,
        string bodyPath,
        HashSet<Component> path)
    {
        if (component == null) return null;
        if (component is ExpressionComponent expression)
        {
            var source = _definitions.Resolve(expression);
            if (source == null) return null;
            if (source != expression) return Resolve((Component)source, bodyPath, path);
        }
        if (!path.Add(component)) return null;
        try
        {
            var data = ReadData(component);
            return data == null ? null : ResolveData(data, bodyPath, path, includeLocal: true);
        }
        finally
        {
            path.Remove(component);
        }
    }

    private FacialBlendShapeData? ReadData(Component component)
    {
        if (component is not ISettingProvider<FacialBlendShapeData>)
            return null;
        var setting = _context.Observe(
            component,
            current =>
            {
                var setting = ((ISettingProvider<FacialBlendShapeData>)current).Setting;
                return (setting.Enabled, Value: setting.Value.Clone());
            },
            (left, right) => left.Enabled == right.Enabled && left.Value.Equals(right.Value));
        return setting.Enabled ? setting.Value : null;
    }

    private BlendShapeWeightAnimationSet ResolveData(
        FacialBlendShapeData data,
        string bodyPath,
        HashSet<Component> path,
        bool includeLocal)
    {
        var result = new BlendShapeWeightAnimationSet();
        foreach (var reference in data.ReferenceAnimations ?? Enumerable.Empty<Transform>())
        {
            if (ResolveReference(reference, bodyPath, path) is { } value)
                result.AddRange(value);
        }
        foreach (var clipData in data.ClipAnimations ?? Enumerable.Empty<FacialClipBlendShapeData>())
        {
            if (clipData?.Clip is not { } clip) continue;
            _context.Observe(clip).GetBlendShapeAnimations(
                clipData.ClipOption,
                result,
                bodyPath);
        }
        if (includeLocal)
            result.AddRange(data.BlendShapeAnimations ?? Enumerable.Empty<BlendShapeWeightAnimation>());
        return result;
    }

    private BlendShapeWeightAnimationSet? ResolveReference(
        Transform? source,
        string bodyPath,
        HashSet<Component> path)
    {
        if (source == null) return null;
        BlendShapeWeightAnimationSet? selected = null;
        foreach (var component in _context.GetComponents<FaceTuneTagComponent>(source.gameObject))
        {
            if (component is not ISettingProvider<FacialBlendShapeData>) continue;
            if (Resolve(component, bodyPath, path) is { } value) selected = value;
        }
        return selected;
    }
}

internal sealed class NonFacialAnimationResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;
    private readonly ExpressionDefinitionResolver _definitions;

    public NonFacialAnimationResolver(GameObject root, ComputeContext? context = null)
    {
        _root = root;
        _context = context ?? ComputeContext.NullContext;
        _definitions = new ExpressionDefinitionResolver(context);
    }

    public ResolvedNonFacialAnimationSet Resolve(
        ExpressionComponent expression,
        string bodyPath)
        => Resolve((Component)expression, bodyPath, new HashSet<Component>())
           ?? new ResolvedNonFacialAnimationSet();

    public ResolvedNonFacialAnimationSet ResolveDefinition(
        Component? source,
        string bodyPath)
        => Resolve(source, bodyPath, new HashSet<Component>())
           ?? new ResolvedNonFacialAnimationSet();

    private ResolvedNonFacialAnimationSet? Resolve(
        Component? component,
        string bodyPath,
        HashSet<Component> path)
    {
        if (component == null) return null;
        if (component is ExpressionComponent expression)
        {
            var source = _definitions.Resolve(expression);
            if (source == null) return null;
            if (source != expression) return Resolve((Component)source, bodyPath, path);
        }
        if (component is not ISettingProvider<NonFacialAnimationData>
            || !path.Add(component))
            return null;
        try
        {
            var data = ReadData(component);
            return data == null ? null : ResolveData(data, component, bodyPath, path);
        }
        finally
        {
            path.Remove(component);
        }
    }

    private NonFacialAnimationData? ReadData(Component owner)
    {
        var setting = _context.Observe(
            owner,
            component =>
            {
                var setting = ((ISettingProvider<NonFacialAnimationData>)component).Setting;
                return (setting.Enabled, Value: setting.Value.Clone(component));
            },
            (left, right) => left.Enabled == right.Enabled && left.Value.Equals(right.Value));
        return setting.Enabled ? setting.Value : null;
    }

    private ResolvedNonFacialAnimationSet ResolveData(
        NonFacialAnimationData data,
        Component owner,
        string bodyPath,
        HashSet<Component> path)
    {
        var result = new ResolvedNonFacialAnimationSet();
        foreach (var reference in data.ReferenceAnimations ?? Enumerable.Empty<Transform>())
        {
            if (ResolveReference(reference, bodyPath, path) is { } value)
                Add(result, value);
        }
        foreach (var clip in data.AnimationClips ?? Enumerable.Empty<AnimationClip>())
            AddClip(result, clip, bodyPath);
        foreach (var animation in data.TransformAnimations ?? Enumerable.Empty<TransformAnimation>())
        {
            if (animation == null) continue;
            var target = animation.Target.Get(owner);
            var pathName = target == null ? null : Utils.GetRelativePath(_root, target);
            if (pathName == null) continue;
            result.AddFloatCurve(
                EditorCurveBinding.FloatCurve(pathName, typeof(GameObject), "m_IsActive"),
                animation.Curve ?? AnimationCurve.Constant(0f, 1f, 1f));
        }
        return result;
    }

    private ResolvedNonFacialAnimationSet? ResolveReference(
        Transform? source,
        string bodyPath,
        HashSet<Component> path)
    {
        if (source == null) return null;
        ResolvedNonFacialAnimationSet? selected = null;
        foreach (var component in _context.GetComponents<FaceTuneTagComponent>(source.gameObject))
        {
            if (component is not ISettingProvider<NonFacialAnimationData>) continue;
            if (Resolve(component, bodyPath, path) is { } value) selected = value;
        }
        return selected;
    }

    private void AddClip(
        ResolvedNonFacialAnimationSet result,
        AnimationClip? clip,
        string bodyPath)
    {
        if (clip == null) return;
        var observedClip = _context.Observe(clip);
        foreach (var binding in AnimationUtility.GetCurveBindings(observedClip))
        {
            if (binding.path == bodyPath
                && binding.type == typeof(SkinnedMeshRenderer)
                && binding.propertyName.StartsWith(
                    FaceTuneConstants.BlendShapePropertyPrefix,
                    StringComparison.Ordinal))
                continue;
            var curve = AnimationUtility.GetEditorCurve(observedClip, binding);
            if (curve != null) result.AddFloatCurve(binding, curve);
        }
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(observedClip))
        {
            result.AddObjectCurve(
                binding,
                AnimationUtility.GetObjectReferenceCurve(observedClip, binding));
        }
    }

    private static void Add(
        ResolvedNonFacialAnimationSet target,
        ResolvedNonFacialAnimationSet source)
    {
        foreach (var (binding, curve) in source.FloatCurves) target.AddFloatCurve(binding, curve);
        foreach (var (binding, curve) in source.ObjectCurves) target.AddObjectCurve(binding, curve);
    }
}