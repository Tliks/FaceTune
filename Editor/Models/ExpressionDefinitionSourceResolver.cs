using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

/// <summary>Whole Reference chainを辿り、Definitionを保持するComponentを返す。</summary>
internal sealed class ExpressionDefinitionSourceResolver
{
    private readonly ComputeContext _context;

    public ExpressionDefinitionSourceResolver(ComputeContext? context = null)
    {
        _context = context ?? ComputeContext.NullContext;
    }

    public Component? Find(ExpressionComponent expression)
        => Find(expression, new HashSet<ExpressionComponent>());

    private Component? Find(
        ExpressionComponent expression,
        HashSet<ExpressionComponent> path)
    {
        if (!path.Add(expression)) return null;
        var reference = _context.Observe(
            expression,
            value => (value.ExpressionDataReference.Mode, value.ExpressionDataReference.Source),
            (left, right) => left == right);
        if (reference.Mode == SettingsReferenceMode.Direct) return expression;
        var source = FindSource(reference.Source);
        return source is ExpressionComponent referencedExpression
            ? Find(referencedExpression, path)
            : source;
    }

    private Component? FindSource(Transform? source)
    {
        if (source == null) return null;
        Component? selected = null;
        foreach (var component in _context.GetComponents<FaceTuneTagComponent>(source.gameObject))
        {
            if (component is ExpressionComponent or ExpressionDataComponent)
                selected = component;
        }
        return selected;
    }
}

internal sealed class FacialAnimationResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;
    private readonly ExpressionDefinitionSourceResolver _sources;

    public FacialAnimationResolver(GameObject root, ComputeContext? context = null)
    {
        _root = root;
        _context = context ?? ComputeContext.NullContext;
        _sources = new ExpressionDefinitionSourceResolver(context);
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
            var source = _sources.Find(expression);
            if (source == null) return null;
            if (source != expression) return Resolve(source, bodyPath, path);
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
        => component switch
        {
            ExpressionComponent expression => Observe(
                expression,
                value => value.FacialBlendShapes),
            ExpressionDataComponent data when _context.Observe(
                data,
                value => value.HasFacialBlendShapes,
                (left, right) => left == right) => Observe(
                    data,
                    value => value.FacialBlendShapes),
            SettingsComponent settings when _context.Observe(
                settings,
                value => value.HasFacialBlendShapes,
                (left, right) => left == right) => Observe(
                    settings,
                    value => value.FacialBlendShapes),
            _ => null
        };

    private FacialBlendShapeData Observe<TComponent>(
        TComponent owner,
        Func<TComponent, FacialBlendShapeData> getData)
        where TComponent : Component
        => _context.Observe(
            owner,
            component => getData(component).Clone(),
            (left, right) => left.Equals(right));

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
            if (component is not IReferenceableExpressionSettings<FacialBlendShapeData>) continue;
            if (Resolve(component, bodyPath, path) is { } value) selected = value;
        }
        return selected;
    }
}

internal sealed class NonFacialAnimationResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;
    private readonly ExpressionDefinitionSourceResolver _sources;

    public NonFacialAnimationResolver(GameObject root, ComputeContext? context = null)
    {
        _root = root;
        _context = context ?? ComputeContext.NullContext;
        _sources = new ExpressionDefinitionSourceResolver(context);
    }

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
            var source = _sources.Find(expression);
            if (source == null) return null;
            if (source != expression) return Resolve(source, bodyPath, path);
        }
        if (component is not IReferenceableExpressionSettings<NonFacialAnimationData>
            || component is not (ExpressionComponent or ExpressionDataComponent)
            || component is ExpressionDataComponent expressionData
               && !_context.Observe(
                   expressionData,
                   value => value.HasNonFacialAnimations,
                   (left, right) => left == right)
            || !path.Add(component))
            return null;
        try
        {
            var data = Observe(component);
            return ResolveData(data, component, bodyPath, path);
        }
        finally
        {
            path.Remove(component);
        }
    }

    private NonFacialAnimationData Observe(Component owner)
        => _context.Observe(
            owner,
            component => component switch
            {
                ExpressionComponent expression => expression.NonFacialAnimations.Clone(expression),
                ExpressionDataComponent data => data.NonFacialAnimations.Clone(data),
                _ => throw new ArgumentOutOfRangeException(nameof(component))
            },
            (left, right) => left.Equals(right));

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
            if (component is not IReferenceableExpressionSettings<NonFacialAnimationData>) continue;
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

internal sealed class ReferenceableSettingResolver<TValue> where TValue : class
{
    private readonly ComputeContext _context;
    private readonly ExpressionDefinitionSourceResolver _sources;
    private readonly Func<TValue, TValue> _snapshot;

    public ReferenceableSettingResolver(
        Func<TValue, TValue> snapshot,
        ComputeContext? context = null)
    {
        _snapshot = snapshot;
        _context = context ?? ComputeContext.NullContext;
        _sources = new ExpressionDefinitionSourceResolver(context);
    }

    public TValue? Resolve(Component component)
        => Resolve(component, new HashSet<Component>());

    private TValue? Resolve(Component? component, HashSet<Component> path)
    {
        if (component == null) return null;
        if (component is ExpressionComponent expression)
        {
            var source = _sources.Find(expression);
            if (source == null) return null;
            if (source != expression) return Resolve(source, path);
        }
        if (component is not IReferenceableExpressionSettings<TValue> || !path.Add(component))
            return null;
        try
        {
            var settings = _context.Observe(
                component,
                current =>
                {
                    var value = ((IReferenceableExpressionSettings<TValue>)current).Settings;
                    return new ReferenceableExpressionSettings<TValue>(
                        value.Enabled,
                        value.Mode,
                        value.Source,
                        _snapshot(value.Direct));
                },
                (left, right) => left.Equals(right));
            if (!settings.Enabled) return null;
            return settings.Mode == SettingsReferenceMode.Direct
                ? settings.Direct
                : ResolveReference(settings.Source, path);
        }
        finally
        {
            path.Remove(component);
        }
    }

    private TValue? ResolveReference(Transform? source, HashSet<Component> path)
    {
        if (source == null) return null;
        TValue? selected = null;
        foreach (var component in _context.GetComponents<FaceTuneTagComponent>(source.gameObject))
        {
            if (component is not IReferenceableExpressionSettings<TValue>) continue;
            if (Resolve(component, path) is { } value) selected = value;
        }
        return selected;
    }
}
