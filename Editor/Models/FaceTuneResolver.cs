using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal sealed class FaceTuneResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext? _context;
    private FaceTuneFacialDataResolver? _facialData;
    private FaceTuneScopedResolver<EyeBlinkSettings>? _eyeBlink;
    private FaceTuneScopedResolver<LipSyncSettings>? _lipSync;
    private FaceTuneScopedResolver<TransitionSettings>? _transition;
    private FaceTuneScopedResolver<PrioritySettings>? _priority;
    private FaceTuneSettingsReferenceResolver? _settingsReferences;
    private FaceTuneMenuResolver? _menus;

    public FaceTuneFacialDataResolver FacialData
        => _facialData ??= new FaceTuneFacialDataResolver(_root, SettingsReferences, _context);

    public FaceTuneSettingsReferenceResolver SettingsReferences
        => _settingsReferences ??= new FaceTuneSettingsReferenceResolver();

    public FaceTuneScopedResolver<EyeBlinkSettings> EyeBlink => _eyeBlink ??= new(
        setting => SettingsReferences.TryResolve<EyeBlinkSettings>(setting, out var value) ? value : null,
        expression => SettingsReferences.TryResolve<EyeBlinkSettings>(expression, out var value) ? value : null,
        static () => new EyeBlinkSettings(),
        static (_, next) => next);

    public FaceTuneScopedResolver<LipSyncSettings> LipSync => _lipSync ??= new(
        setting => SettingsReferences.TryResolve<LipSyncSettings>(setting, out var value) ? value : null,
        expression => SettingsReferences.TryResolve<LipSyncSettings>(expression, out var value) ? value : null,
        static () => new LipSyncSettings(),
        static (_, next) => next);

    public FaceTuneScopedResolver<TransitionSettings> Transition => _transition ??= new(
        setting => setting.HasTransition ? setting.Transition : null,
        expression => expression.HasTransition ? expression.Transition : null,
        static () => new TransitionSettings(),
        static (_, next) => next);

    public FaceTuneScopedResolver<PrioritySettings> Priority => _priority ??= new(
        setting => setting.HasPriority ? setting.Priority : null,
        expression => expression.HasPriority ? expression.Priority : null,
        static () => new PrioritySettings(),
        static (_, next) => next);

    public FaceTuneMenuResolver Menus
        => _menus ??= new FaceTuneMenuResolver(_root);

    public FaceTuneResolver(GameObject root, ComputeContext? context = null)
    {
        _root = root;
        _context = context;
    }
}

internal sealed class FaceTuneFacialDataResolver
{
    private readonly GameObject _root;
    private readonly FaceTuneSettingsReferenceResolver _references;
    private readonly ComputeContext _context;

    internal FaceTuneFacialDataResolver(
        GameObject root,
        FaceTuneSettingsReferenceResolver references,
        ComputeContext? context)
    {
        _root = root;
        _references = references;
        _context = context ?? ComputeContext.NullContext;
    }

    public IEnumerable<(SettingsComponent Owner, FacialBlendShapeData Value)> EnumerateIncoming(Component target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);
        for (var i = settings.Length - 1; i >= 0; i--)
        {
            var owner = _context.Observe(settings[i]);
            if (owner.HasFacialBlendShapes && _references.TryResolve<FacialBlendShapeData>(owner, out var value, component => _context.Observe(component)))
                yield return (owner, value);
        }
    }

    public IEnumerable<(Component Owner, FacialBlendShapeData Value)> EnumerateLocal(ExpressionComponent expression)
    {
        var owner = _context.Observe(expression);
        if (_references.TryResolve<FacialBlendShapeData>(owner, out var value, component => _context.Observe(component)))
            yield return (owner, value);

        foreach (var data in _context.GetComponentsInChildren<ExpressionDataComponent>(expression.gameObject, true))
        {
            var dataOwner = _context.Observe(data);
            if (_references.TryResolve<FacialBlendShapeData>(dataOwner, out var dataValue, component => _context.Observe(component)))
                yield return (dataOwner, dataValue);
        }
    }

    public IEnumerable<(Component Owner, FacialBlendShapeData Value)> Enumerate(ExpressionComponent expression)
    {
        foreach (var item in EnumerateIncoming(expression)) yield return item;
        foreach (var item in EnumerateLocal(expression)) yield return item;
    }

    public void AddIncoming(Component target, ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        foreach (var (_, value) in EnumerateIncoming(target))
            AddAnimations(value, result, bodyPath);
    }

    public void AddLocal(ExpressionComponent expression, ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        foreach (var (_, value) in EnumerateLocal(expression))
            AddAnimations(value, result, bodyPath);
    }

    public void Add(ExpressionComponent expression, ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        foreach (var (_, value) in Enumerate(expression))
            AddAnimations(value, result, bodyPath);
    }

    public void AddRenderer(ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        foreach (var settings in _context.GetComponentsInChildren<SettingsComponent>(_root, true))
        {
            var owner = _context.Observe(settings);
            if (owner.HasFacialBlendShapes && owner.ApplyToRenderer
                && _references.TryResolve<FacialBlendShapeData>(owner, out var value, component => _context.Observe(component)))
                AddAnimations(value, result, bodyPath);
        }
    }

    private static void AddAnimations(FacialBlendShapeData data, ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        if (data.Clip != null)
            data.Clip.GetBlendShapeAnimations(data.ClipOption, result, bodyPath);

        foreach (var animation in data.BlendShapeAnimations)
            result.Add(animation);
    }
}

internal sealed class FaceTuneScopedResolver<TValue> where TValue : class
{
    private readonly Func<SettingsComponent, TValue?> _getSettings;
    private readonly Func<ExpressionComponent, TValue?> _getExpression;
    private readonly Func<TValue> _getDefault;
    private readonly Func<TValue, TValue, TValue> _merge;

    internal FaceTuneScopedResolver(
        Func<SettingsComponent, TValue?> getSettings,
        Func<ExpressionComponent, TValue?> getExpression,
        Func<TValue> getDefault,
        Func<TValue, TValue, TValue> merge)
    {
        _getSettings = getSettings;
        _getExpression = getExpression;
        _getDefault = getDefault;
        _merge = merge;
    }

    public IEnumerable<(SettingsComponent Owner, TValue Value)> EnumerateIncoming(Component target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);
        for (var i = settings.Length - 1; i >= 0; i--)
        {
            var owner = settings[i];
            if (_getSettings(owner) is { } value)
                yield return (owner, value);
        }
    }

    public IEnumerable<(Component Owner, TValue Value)> Enumerate(ExpressionComponent expression)
    {
        foreach (var (owner, incomingValue) in EnumerateIncoming(expression))
            yield return (owner, incomingValue);
        if (_getExpression(expression) is { } value)
            yield return (expression, value);
    }

    public TValue GetIncoming(Component target) => GetIncoming(target, out _);

    public TValue GetIncoming(Component target, out SettingsComponent? lastOwner)
    {
        var value = _getDefault();
        lastOwner = null;
        foreach (var (owner, resolved) in EnumerateIncoming(target))
        {
            lastOwner = owner;
            value = _merge(value, resolved);
        }
        return value;
    }

    public TValue Get(ExpressionComponent expression) => Get(expression, out _);

    public TValue Get(ExpressionComponent expression, out Component? lastOwner)
    {
        var value = _getDefault();
        lastOwner = null;
        foreach (var (owner, resolved) in Enumerate(expression))
        {
            lastOwner = owner;
            value = _merge(value, resolved);
        }
        return value;
    }
}


internal sealed class FaceTuneSettingsReferenceResolver
{
    public bool TryResolve<TValue>(
        Component owner,
        [NotNullWhen(true)] out TValue? value,
        Action<Component>? observe = null)
        where TValue : class
    {
        observe?.Invoke(owner);
        if (owner is not IReferenceableExpressionSettings<TValue> referenceable
            || !referenceable.Settings.Enabled)
        {
            value = null;
            return false;
        }
        return TryResolve(referenceable.Settings, new HashSet<Component> { owner }, observe, out value);
    }

    private static bool TryResolve<TValue>(
        ReferenceableExpressionSettings<TValue> source,
        HashSet<Component> visited,
        Action<Component>? observe,
        [NotNullWhen(true)] out TValue? value)
        where TValue : class
    {
        if (source.Mode == SettingsReferenceMode.Direct)
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
        ReferenceableExpressionSettings<TValue>? referencedValue = null;
        foreach (var component in source.Source.GetComponents<FaceTuneTagComponent>())
        {
            observe?.Invoke(component);
            if (component is not IReferenceableExpressionSettings<TValue> referenceable)
                continue;
            var candidate = referenceable.Settings;
            if (!candidate.Enabled)
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

        return TryResolve(referencedValue.Value, visited, observe, out value);
    }
}

internal sealed class FaceTuneMenuResolver
{
    private readonly Transform _root;

    internal FaceTuneMenuResolver(GameObject root) => _root = root.transform;

    public IEnumerable<MenuComponent> EnumerateFolders(Component target)
    {
        if (target.transform != _root && !target.transform.IsChildOf(_root))
            yield break;

        var menus = target.GetComponentsInParentExcludingSelf<MenuComponent>(true);
        for (var i = menus.Length - 1; i >= 0; i--)
        {
            var menu = menus[i];
            if ((menu.transform == _root || menu.transform.IsChildOf(_root)) && menu.MenuKind == MenuComponent.Kind.Folder)
                yield return menu;
        }
    }

    public Transform? GetInstallTarget(Component owner, MenuSettings menu)
    {
        if (owner.transform != _root && !owner.transform.IsChildOf(_root))
            return null;
        if (menu.InstallContainer != null && (menu.InstallContainer == _root || menu.InstallContainer.IsChildOf(_root)))
            return menu.InstallContainer;
        if (menu.InstallContainer != null)
            return null;
        var folder = EnumerateFolders(owner)
            .LastOrDefault()
            .DestroyedAsNull();
        return folder == null ? null : folder.transform.DestroyedAsNull();
    }
}

