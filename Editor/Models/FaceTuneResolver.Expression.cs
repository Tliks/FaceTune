using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal sealed class ExpressionDefinitionResolver
{
    private readonly ComputeContext context;

    public ExpressionDefinitionResolver(ComputeContext? context = null)
    {
        this.context = context ?? ComputeContext.NullContext;
    }

    public IExpressionDefinitionProvider? Resolve(IExpressionDefinitionProvider expression)
        => Resolve(expression, new HashSet<Component>());

    private IExpressionDefinitionProvider? Resolve(
        IExpressionDefinitionProvider provider,
        HashSet<Component> path)
    {
        var component = provider.Component;
        if (!path.Add(component)) return null;
        try
        {
            if (provider is not IExpressionDefinitionProviderWithReference) return provider;
            var reference = context.Observe(
                component,
                static current => ((IExpressionDefinitionProviderWithReference)current).Reference,
                static (left, right) => left == right);
            if (reference.Mode == SettingsReferenceMode.Direct) return provider;
            return reference.Source is IExpressionDefinitionProvider source
                ? Resolve(source, path)
                : null;
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

    public ExpressionBehavior Resolve(IExpressionDefinitionProvider expression)
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

    public MultiFrameSettings Resolve(IExpressionDefinitionProvider expression)
        => definitions.Resolve(expression) is ISettingProvider<MultiFrameSettings> provider
            ? ResolveProvider(provider) ?? new MultiFrameSettings()
            : new MultiFrameSettings();

    public MultiFrameSettings? ResolveProvider(ISettingProvider<MultiFrameSettings> provider)
        => values.Resolve(provider);
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

    public EyeBlinkSettings? ResolveDefinition(IExpressionDefinitionProvider expression)
        => definitions.Resolve(expression) is ISettingProvider<EyeBlinkSettings> provider
            ? references.Resolve(provider)
            : null;

    public EyeBlinkSettings? ResolveProvider(ISettingProvider<EyeBlinkSettings> provider)
        => references.Resolve(provider);

    public ScopedValue<EyeBlinkSettings> ResolveIncoming(Component component)
        => scope.GetIncoming(component);

    public ScopedValue<EyeBlinkSettings> ResolveInherited(ExpressionComponent expression)
        => ResolveIncoming(expression);

    public EyeBlinkSettings Resolve(ExpressionComponent expression)
        => ResolveDefinition(expression) ?? ResolveIncoming(expression).Value;
}

internal sealed class LipSyncResolver
{
    private readonly ExpressionDefinitionResolver definitions;
    private readonly SettingValueResolver<LipSyncSettings> references;
    private readonly ScopedValueResolver<LipSyncSettings> scope;

    public LipSyncResolver(GameObject root, ComputeContext? context = null)
    {
        definitions = new ExpressionDefinitionResolver(context);
        references = new SettingValueResolver<LipSyncSettings>(static (_, value) => Clone(value), context);
        scope = new ScopedValueResolver<LipSyncSettings>(
            root,
            settings => references.Resolve(settings),
            static () => new LipSyncSettings(),
            context);
    }

    public LipSyncSettings? ResolveDefinition(IExpressionDefinitionProvider expression)
        => definitions.Resolve(expression) is ISettingProvider<LipSyncSettings> provider
            ? references.Resolve(provider)
            : null;

    public LipSyncSettings? ResolveProvider(ISettingProvider<LipSyncSettings> provider)
        => references.Resolve(provider);

    public ScopedValue<LipSyncSettings> ResolveIncoming(Component component)
        => scope.GetIncoming(component);

    public ScopedValue<LipSyncSettings> ResolveInherited(ExpressionComponent expression)
        => ResolveIncoming(expression);

    public LipSyncSettings Resolve(ExpressionComponent expression)
        => ResolveDefinition(expression) ?? ResolveIncoming(expression).Value;

    private static LipSyncSettings Clone(LipSyncSettings value)
        => new()
        {
            Mode = value.Mode,
            CancellerBlendShapes = value.CancellerBlendShapes.ToList(),
            Shapes = new VrcVisemeLipSyncShapes
            {
                Sil = value.Shapes.Sil.ToList(),
                PP = value.Shapes.PP.ToList(),
                FF = value.Shapes.FF.ToList(),
                TH = value.Shapes.TH.ToList(),
                DD = value.Shapes.DD.ToList(),
                KK = value.Shapes.KK.ToList(),
                CH = value.Shapes.CH.ToList(),
                SS = value.Shapes.SS.ToList(),
                NN = value.Shapes.NN.ToList(),
                RR = value.Shapes.RR.ToList(),
                AA = value.Shapes.AA.ToList(),
                E = value.Shapes.E.ToList(),
                IH = value.Shapes.IH.ToList(),
                OH = value.Shapes.OH.ToList(),
                OU = value.Shapes.OU.ToList()
            }
        };
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
        ISettingProvider<FacialBlendShapeData> provider,
        [NotNullWhen(true)] out BlendShapeWeightAnimationSet? value)
    {
        value = Resolve(provider, new HashSet<Component>());
        return value != null;
    }

    public bool TryResolveBase(
        ISettingProvider<FacialBlendShapeData> provider,
        [NotNullWhen(true)] out BlendShapeWeightAnimationSet? value)
    {
        var data = ReadData(provider);
        if (data == null)
        {
            value = null;
            return false;
        }
        value = ResolveData(data, new HashSet<Component>(), includeLocal: false);
        return true;
    }

    public bool TryResolveCompositeBase(
        ISettingProvider<FacialBlendShapeData> provider,
        int entryIndex,
        [NotNullWhen(true)] out BlendShapeWeightAnimationSet? value)
    {
        var data = ReadData(provider);
        if (data == null || data.BlendShapeMode != FacialBlendShapeData.Mode.Composite)
        {
            value = null;
            return false;
        }
        value = ResolveData(data, new HashSet<Component>(), true, entryIndex);
        return true;
    }

    public BlendShapeWeightAnimationSet ResolveIncoming(Transform target)
    {
        var result = new BlendShapeWeightAnimationSet();
        foreach (var settings in _context.GetComponentsInParentExcludingSelf<SettingsComponent>(
                     _root,
                     target,
                     true))
        {
            if (Resolve(settings, new HashSet<Component>()) is { } value)
                result.AddRange(value);
        }
        return result;
    }

    public void AddRenderer(ICollection<BlendShapeWeightAnimation> result)
    {
        foreach (var settings in _context.GetComponentsInChildren<SettingsComponent>(_root, true))
        {
            var enabled = _context.Observe(
                settings,
                static value => value.ApplyToRenderer,
                static (left, right) => left == right);
            if (!enabled || Resolve(settings, new HashSet<Component>()) is not { } value)
                continue;
            foreach (var animation in value) result.Add(animation);
        }
    }

    private BlendShapeWeightAnimationSet? Resolve(
        ISettingProvider<FacialBlendShapeData> provider,
        HashSet<Component> path)
    {
        if (provider is IExpressionDefinitionProvider definition)
        {
            var source = _definitions.Resolve(definition);
            if (source == null) return null;
            if (source != definition)
                return source is ISettingProvider<FacialBlendShapeData> resolved
                    ? Resolve(resolved, path)
                    : null;
        }
        var component = provider.Component;
        if (!path.Add(component)) return null;
        try
        {
            var data = ReadData(provider);
            return data == null ? null : ResolveData(data, path, includeLocal: true);
        }
        finally
        {
            path.Remove(component);
        }
    }

    private FacialBlendShapeData? ReadData(ISettingProvider<FacialBlendShapeData> provider)
    {
        var setting = _context.Observe(
            provider.Component,
            static component =>
            {
                var current = ((ISettingProvider<FacialBlendShapeData>)component).Setting;
                return (current.Enabled, Value: current.Value.Clone());
            },
            static (left, right) => left.Enabled == right.Enabled && left.Value.Equals(right.Value));
        return setting.Enabled ? setting.Value : null;
    }

    private BlendShapeWeightAnimationSet ResolveData(
        FacialBlendShapeData data,
        HashSet<Component> path,
        bool includeLocal,
        int? compositeEntryLimit = null)
    {
        var result = new BlendShapeWeightAnimationSet();
        if (data.BlendShapeMode == FacialBlendShapeData.Mode.Simple)
            AddSimple(result, data, path, includeLocal);
        else
            AddComposite(result, data.CompositeEntries, path, compositeEntryLimit);
        return result;
    }

    private void AddSimple(
        BlendShapeWeightAnimationSet result,
        FacialBlendShapeData data,
        HashSet<Component> path,
        bool includeLocal)
    {
        if (data.BaseSource == FacialBlendShapeData.SimpleBaseSource.Clip)
            AddClip(result, data.Clip, data.ClipOption);
        else if (ResolveReference(data.ReferenceSource, path) is { } reference)
            result.AddRange(reference);
        if (includeLocal)
            result.AddRange(data.BlendShapeAnimations ?? Enumerable.Empty<BlendShapeWeightAnimation>());
    }

    private void AddComposite(
        BlendShapeWeightAnimationSet result,
        List<FacialBlendShapeData.CompositeEntry>? entries,
        HashSet<Component> path,
        int? entryLimit)
    {
        entries ??= new List<FacialBlendShapeData.CompositeEntry>();
        var count = Mathf.Min(entryLimit ?? entries.Count, entries.Count);
        for (var index = 0; index < count; index++)
        {
            var entry = entries[index];
            if (entry == null) continue;
            switch (entry.EntryKind)
            {
                case FacialBlendShapeData.CompositeEntry.Kind.Direct:
                    result.AddRange(entry.BlendShapeAnimations ?? Enumerable.Empty<BlendShapeWeightAnimation>());
                    break;
                case FacialBlendShapeData.CompositeEntry.Kind.Clip:
                    AddClip(result, entry.Clip, entry.ClipOption);
                    break;
                case FacialBlendShapeData.CompositeEntry.Kind.Reference:
                    if (ResolveReference(entry.ReferenceSource, path) is { } reference)
                        result.AddRange(reference);
                    break;
            }
        }
    }

    private void AddClip(
        ICollection<BlendShapeWeightAnimation> result,
        AnimationClip? clip,
        ClipImportOption option)
    {
        if (clip != null)
            _context.Observe(clip).GetBlendShapeAnimations(option, result, string.Empty);
    }

    private BlendShapeWeightAnimationSet? ResolveReference(
        FaceTuneTagComponent? source,
        HashSet<Component> path)
        => source is ISettingProvider<FacialBlendShapeData> provider
            ? Resolve(provider, path)
            : null;
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
        ISettingProvider<NonFacialAnimationData> provider,
        string bodyPath)
        => Resolve(provider, bodyPath, new HashSet<Component>())
           ?? new ResolvedNonFacialAnimationSet();

    private ResolvedNonFacialAnimationSet? Resolve(
        ISettingProvider<NonFacialAnimationData> provider,
        string bodyPath,
        HashSet<Component> path)
    {
        if (provider is IExpressionDefinitionProvider definition)
        {
            var source = _definitions.Resolve(definition);
            if (source == null) return null;
            if (source != definition)
                return source is ISettingProvider<NonFacialAnimationData> resolved
                    ? Resolve(resolved, bodyPath, path)
                    : null;
        }
        var component = provider.Component;
        if (!path.Add(component)) return null;
        try
        {
            var data = ReadData(provider);
            return data == null ? null : ResolveData(data, component, bodyPath, path);
        }
        finally
        {
            path.Remove(component);
        }
    }

    private NonFacialAnimationData? ReadData(ISettingProvider<NonFacialAnimationData> provider)
    {
        var setting = _context.Observe(
            provider.Component,
            static component =>
            {
                var current = ((ISettingProvider<NonFacialAnimationData>)component).Setting;
                return (current.Enabled, Value: current.Value.Clone(component));
            },
            static (left, right) => left.Enabled == right.Enabled && left.Value.Equals(right.Value));
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
            if (component is ISettingProvider<NonFacialAnimationData> provider
                && Resolve(provider, bodyPath, path) is { } value)
                selected = value;
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