using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

/// <summary>FaceTuneの各Resolverを同じアバタールートで利用する入口。</summary>
internal sealed class FaceTuneResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;
    private FaceTuneConditionResolver? _conditions;
    private FaceTuneFacialDataResolver? _facialData;
    private FaceTuneExpressionSettingsResolver? _expressionSettings;
    private FaceTuneSettingsSourceResolver? _settingsSources;
    private FaceTuneMenuResolver? _menus;

    public FaceTuneConditionResolver Conditions
        => _conditions ??= new FaceTuneConditionResolver(_root, _context);

    public FaceTuneFacialDataResolver FacialData
        => _facialData ??= new FaceTuneFacialDataResolver(_root, _context, SettingsSources);

    public FaceTuneSettingsSourceResolver SettingsSources
        => _settingsSources ??= new FaceTuneSettingsSourceResolver(_context);

    public FaceTuneExpressionSettingsResolver ExpressionSettings
        => _expressionSettings ??= new FaceTuneExpressionSettingsResolver(_root, _context, SettingsSources);

    public FaceTuneMenuResolver Menus
        => _menus ??= new FaceTuneMenuResolver(_root, _context);

    public FaceTuneResolver(GameObject root, ComputeContext? context = null)
    {
        _root = root;
        _context = context ?? ComputeContext.NullContext;
    }
}

internal sealed class FaceTuneConditionResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;

    internal FaceTuneConditionResolver(GameObject root, ComputeContext context)
    {
        _root = root;
        _context = context;
    }

    /// <summary>アバタールートから対象までにあるSettingsの条件を親側から返す。</summary>
    public IEnumerable<(SettingsComponent Source, Condition Value)> Enumerate(Transform target)
    {
        using var _ = ListPool<SettingsComponent>.Get(out var settings);
        _context.GetComponentsInParent(target.gameObject, _root, true, settings);

        for (var i = settings.Count - 1; i >= 0; i--)
        {
            _context.Observe(settings[i]);
            if (settings[i].HasCondition)
                yield return (settings[i], settings[i].Condition);
        }
    }

    /// <summary>アバタールートから対象までにあるExpression Setを親側から返す。</summary>
    public IEnumerable<(SettingsComponent Source, ExpressionSetSettings Value)> EnumerateExpressionSets(
        Transform target)
    {
        using var _ = ListPool<SettingsComponent>.Get(out var settings);
        _context.GetComponentsInParent(target.gameObject, _root, true, settings);

        for (var i = settings.Count - 1; i >= 0; i--)
        {
            _context.Observe(settings[i]);
            if (settings[i].ExpressionSetEnabled)
                yield return (settings[i], settings[i].ExpressionSet);
        }
    }
}

internal sealed class FaceTuneFacialDataResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;
    private readonly FaceTuneSettingsSourceResolver _sources;

    internal FaceTuneFacialDataResolver(
        GameObject root,
        ComputeContext context,
        FaceTuneSettingsSourceResolver sources)
    {
        _root = root;
        _context = context;
        _sources = sources;
    }

    /// <summary>
    /// 親のSettings、Expression自身、配下のExpressionDataComponentの順に返す。
    /// 別のExpressionの配下には入らない。
    /// </summary>
    public IEnumerable<(Component Owner, FacialBlendShapeDataSource Value)> Enumerate(
        ExpressionComponent expression)
    {
        using var _settings = ListPool<SettingsComponent>.Get(out var settings);
        _context.GetComponentsInParent(expression.gameObject, _root, true, settings);
        for (var i = settings.Count - 1; i >= 0; i--)
        {
            _context.Observe(settings[i]);
            if (settings[i].HasFacialBlendShapes)
                yield return (settings[i], settings[i].FacialBlendShapes);
        }

        _context.Observe(expression);
        yield return (expression, expression.FacialBlendShapes);

        foreach (var data in _context.GetComponentsInChildren<ExpressionDataComponent>(
                     expression.gameObject,
                     true))
        {
            _context.Observe(data);
            yield return (data, data.FacialBlendShapes);
        }
    }

    /// <summary>rendererの初期値へ適用する顔データをGameObjectの並び順どおりに返す。</summary>
    public IEnumerable<(SettingsComponent Owner, FacialBlendShapeDataSource Value)> EnumerateForRenderer()
    {
        foreach (var settings in _context.GetComponentsInChildren<SettingsComponent>(_root, true))
        {
            _context.Observe(settings);
            if (settings.HasFacialBlendShapes && settings.ApplyToRenderer)
                yield return (settings, settings.FacialBlendShapes);
        }
    }

    public bool TryResolve(
        FacialBlendShapeDataSource source,
        Component owner,
        [NotNullWhen(true)] out FacialBlendShapeData? value)
        => _sources.TryResolve(source, owner, out value);
}

internal sealed class FaceTuneExpressionSettingsResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;
    private readonly FaceTuneSettingsSourceResolver _sources;

    internal FaceTuneExpressionSettingsResolver(
        GameObject root,
        ComputeContext context,
        FaceTuneSettingsSourceResolver sources)
    {
        _root = root;
        _context = context;
        _sources = sources;
    }

    public bool TryGetEyeBlink(
        ExpressionComponent expression,
        [NotNullWhen(true)] out EyeBlinkSettings? value)
    {
        _context.Observe(expression);
        if (expression.HasEyeBlink)
            return _sources.TryResolve(expression.EyeBlink, expression, out value);

        using var _ = ListPool<SettingsComponent>.Get(out var settings);
        _context.GetComponentsInParent(expression.gameObject, _root, true, settings);
        foreach (var item in settings)
        {
            _context.Observe(item);
            if (item.HasEyeBlink)
                return _sources.TryResolve(item.EyeBlink, item, out value);
        }

        value = new EyeBlinkSettings();
        return true;
    }

    public bool TryGetLipSync(
        ExpressionComponent expression,
        [NotNullWhen(true)] out LipSyncSettings? value)
    {
        _context.Observe(expression);
        if (expression.HasLipSync)
            return _sources.TryResolve(expression.LipSync, expression, out value);

        using var _ = ListPool<SettingsComponent>.Get(out var settings);
        _context.GetComponentsInParent(expression.gameObject, _root, true, settings);
        foreach (var item in settings)
        {
            _context.Observe(item);
            if (item.HasLipSync)
                return _sources.TryResolve(item.LipSync, item, out value);
        }

        value = new LipSyncSettings();
        return true;
    }

    public TransitionSettings GetTransition(ExpressionComponent expression)
    {
        _context.Observe(expression);
        if (expression.HasTransition)
            return expression.Transition;

        using var _ = ListPool<SettingsComponent>.Get(out var settings);
        _context.GetComponentsInParent(expression.gameObject, _root, true, settings);
        foreach (var item in settings)
        {
            _context.Observe(item);
            if (item.HasTransition)
                return item.Transition;
        }

        return new TransitionSettings();
    }

    /// <summary>親のSettingsを親側から返し、最後にExpression自身を返す。</summary>
    public IEnumerable<(Component Source, ParameterDriverSettingsSource Value)> EnumerateParameterDrivers(
        ExpressionComponent expression)
    {
        using var _ = ListPool<SettingsComponent>.Get(out var settings);
        _context.GetComponentsInParent(expression.gameObject, _root, true, settings);
        for (var i = settings.Count - 1; i >= 0; i--)
        {
            _context.Observe(settings[i]);
            if (settings[i].HasParameterDriver)
                yield return (settings[i], settings[i].ParameterDriver);
        }

        _context.Observe(expression);
        if (expression.HasParameterDriver)
            yield return (expression, expression.ParameterDriver);
    }
}

internal sealed class FaceTuneSettingsSourceResolver
{
    private readonly ComputeContext _context;

    internal FaceTuneSettingsSourceResolver(ComputeContext context)
    {
        _context = context;
    }

    public bool TryResolve<TSource, TValue>(
        TSource source,
        Component owner,
        [NotNullWhen(true)] out TValue? value)
        where TSource : class, ISettingsSource<TValue>
        where TValue : class
    {
        _context.Observe(owner);
        return TryResolve(source, new HashSet<Component> { owner }, out value);
    }

    private bool TryResolve<TSource, TValue>(
        TSource source,
        HashSet<Component> visited,
        [NotNullWhen(true)] out TValue? value)
        where TSource : class, ISettingsSource<TValue>
        where TValue : class
    {
        if (source.SourceMode == SettingsSourceMode.Direct)
        {
            value = source.Direct;
            return true;
        }

        if (source.Source == null)
        {
            value = null;
            return false;
        }

        Component? referencedOwner = null;
        TSource? referencedValue = null;
        foreach (var component in _context.GetComponents<FaceTuneTagComponent>(source.Source.gameObject))
        {
            _context.Observe(component);
            if (component is not IReferenceableExpressionSettings<TSource> referenceable
                || referenceable.SettingsSource is not { } candidate)
                continue;

            // 同じGameObjectではInspector上で下にあるComponentを使う。
            referencedOwner = component;
            referencedValue = candidate;
        }

        if (referencedOwner == null || referencedValue == null || !visited.Add(referencedOwner))
        {
            value = null;
            return false;
        }

        return TryResolve(referencedValue, visited, out value);
    }
}

internal sealed class FaceTuneMenuResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext _context;

    internal FaceTuneMenuResolver(GameObject root, ComputeContext context)
    {
        _root = root;
        _context = context;
    }

    public Transform? GetInstallTarget(Component owner, MenuSettings menu)
    {
        _context.Observe(owner);
        if (menu.InstallContainer != null)
            return menu.InstallContainer;

        using var _ = ListPool<MenuComponent>.Get(out var menus);
        _context.GetComponentsInParent(owner.gameObject, _root, true, menus);
        return menus
            .FirstOrDefault(m => m != owner && m.MenuKind == MenuComponent.Kind.Folder)?.transform;
    }
}

