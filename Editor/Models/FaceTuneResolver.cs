using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal sealed class FaceTuneResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext? _context;
    private FaceTuneConditionResolver? _conditions;
    private FaceTuneFacialDataResolver? _facialData;
    private FaceTuneEyeBlinkResolver? _eyeBlink;
    private FaceTuneLipSyncResolver? _lipSync;
    private FaceTuneTransitionResolver? _transition;
    private FaceTunePriorityResolver? _priority;
    private FaceTuneParameterDriverResolver? _parameterDrivers;
    private FaceTuneSettingsSourceResolver? _settingsSources;
    private FaceTuneMenuResolver? _menus;

    public FaceTuneConditionResolver Conditions
        => _conditions ??= new FaceTuneConditionResolver();

    public FaceTuneFacialDataResolver FacialData
        => _facialData ??= new FaceTuneFacialDataResolver(_root, SettingsSources, _context);

    public FaceTuneSettingsSourceResolver SettingsSources
        => _settingsSources ??= new FaceTuneSettingsSourceResolver();

    public FaceTuneEyeBlinkResolver EyeBlink => _eyeBlink ??= new FaceTuneEyeBlinkResolver(SettingsSources);

    public FaceTuneLipSyncResolver LipSync => _lipSync ??= new FaceTuneLipSyncResolver(SettingsSources);

    public FaceTuneTransitionResolver Transition => _transition ??= new FaceTuneTransitionResolver();

    public FaceTunePriorityResolver Priority => _priority ??= new FaceTunePriorityResolver();

    public FaceTuneParameterDriverResolver ParameterDrivers
        => _parameterDrivers ??= new FaceTuneParameterDriverResolver(SettingsSources);

    public FaceTuneMenuResolver Menus
        => _menus ??= new FaceTuneMenuResolver();

    public FaceTuneResolver(GameObject root, ComputeContext? context = null)
    {
        _root = root;
        _context = context;
    }
}

internal sealed class FaceTuneConditionResolver
{
    public IEnumerable<(SettingsComponent Source, Condition Value)> Enumerate(Transform target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);

        for (var i = settings.Length - 1; i >= 0; i--)
        {
            if (settings[i].HasCondition)
                yield return (settings[i], settings[i].Condition);
        }
    }

    public IEnumerable<(SettingsComponent Source, ExpressionSetSettings Value)> EnumerateExpressionSets(
        Transform target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);

        for (var i = settings.Length - 1; i >= 0; i--)
        {
            if (settings[i].ExpressionSetEnabled)
                yield return (settings[i], settings[i].ExpressionSet);
        }
    }
}

internal sealed class FaceTuneFacialDataResolver
{
    private readonly GameObject _root;
    private readonly FaceTuneSettingsSourceResolver _sources;
    private readonly ComputeContext _context;

    internal FaceTuneFacialDataResolver(
        GameObject root,
        FaceTuneSettingsSourceResolver sources,
        ComputeContext? context)
    {
        _root = root;
        _sources = sources;
        _context = context ?? ComputeContext.NullContext;
    }

    public IEnumerable<(SettingsComponent Owner, FacialBlendShapeData Value)> EnumerateIncoming(Component target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);
        for (var i = settings.Length - 1; i >= 0; i--)
        {
            var owner = _context.Observe(settings[i]);
            if (owner.HasFacialBlendShapes && _sources.TryResolve(owner.FacialBlendShapes, owner, out var value, component => _context.Observe(component)))
                yield return (owner, value);
        }
    }

    public IEnumerable<(Component Owner, FacialBlendShapeData Value)> Enumerate(ExpressionComponent expression)
    {
        foreach (var (incomingOwner, incomingValue) in EnumerateIncoming(expression))
            yield return (incomingOwner, incomingValue);

        var owner = _context.Observe(expression);
        if (_sources.TryResolve(owner.FacialBlendShapes, owner, out var value, component => _context.Observe(component)))
            yield return (owner, value);

        foreach (var data in _context.GetComponentsInChildren<ExpressionDataComponent>(expression.gameObject, true))
        {
            var dataOwner = _context.Observe(data);
            if (_sources.TryResolve(dataOwner.FacialBlendShapes, dataOwner, out var dataValue, component => _context.Observe(component)))
                yield return (dataOwner, dataValue);
        }
    }

    public BlendShapeWeightAnimationSet Get(ExpressionComponent expression, string bodyPath)
    {
        var result = new BlendShapeWeightAnimationSet();
        foreach (var (_, value) in Enumerate(expression))
            AppendAnimations(value, result, bodyPath);
        return result;
    }

    public BlendShapeWeightAnimationSet GetRenderer(string bodyPath)
    {
        var result = new BlendShapeWeightAnimationSet();
        foreach (var settings in _context.GetComponentsInChildren<SettingsComponent>(_root, true))
        {
            var owner = _context.Observe(settings);
            if (owner.HasFacialBlendShapes && owner.ApplyToRenderer
                && _sources.TryResolve(owner.FacialBlendShapes, owner, out var value, component => _context.Observe(component)))
                AppendAnimations(value, result, bodyPath);
        }
        return result;
    }

    private static void AppendAnimations(FacialBlendShapeData data, ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        if (data.Clip is { } clip)
            clip.GetBlendShapeAnimations(data.ClipOption, result, bodyPath);

        foreach (var animation in data.BlendShapeAnimations)
            result.Add(animation);
    }
}

internal sealed class FaceTuneEyeBlinkResolver
{
    private readonly FaceTuneSettingsSourceResolver _sources;

    internal FaceTuneEyeBlinkResolver(FaceTuneSettingsSourceResolver sources) => _sources = sources;

    public IEnumerable<(SettingsComponent Owner, EyeBlinkSettings Value)> EnumerateIncoming(Component target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);
        for (var i = settings.Length - 1; i >= 0; i--)
        {
            var owner = settings[i];
            if (owner.HasEyeBlink && _sources.TryResolve(owner.EyeBlink, owner, out var value))
                yield return (owner, value);
        }
    }

    public IEnumerable<(Component Owner, EyeBlinkSettings Value)> Enumerate(ExpressionComponent expression)
    {
        foreach (var (owner, incomingValue) in EnumerateIncoming(expression))
            yield return (owner, incomingValue);
        if (expression.HasEyeBlink && _sources.TryResolve(expression.EyeBlink, expression, out var value))
            yield return (expression, value);
    }

    public EyeBlinkSettings GetIncoming(Component target, out SettingsComponent? owner)
    {
        var value = new EyeBlinkSettings();
        owner = null;
        foreach (var (source, resolved) in EnumerateIncoming(target))
        {
            owner = source;
            value = resolved;
        }
        return value;
    }

    public EyeBlinkSettings Get(ExpressionComponent expression, out Component? owner)
    {
        var value = new EyeBlinkSettings();
        owner = null;
        foreach (var (source, resolved) in Enumerate(expression))
        {
            owner = source;
            value = resolved;
        }
        return value;
    }
}

internal sealed class FaceTuneLipSyncResolver
{
    private readonly FaceTuneSettingsSourceResolver _sources;

    internal FaceTuneLipSyncResolver(FaceTuneSettingsSourceResolver sources) => _sources = sources;

    public IEnumerable<(SettingsComponent Owner, LipSyncSettings Value)> EnumerateIncoming(Component target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);
        for (var i = settings.Length - 1; i >= 0; i--)
        {
            var owner = settings[i];
            if (owner.HasLipSync && _sources.TryResolve(owner.LipSync, owner, out var value))
                yield return (owner, value);
        }
    }

    public IEnumerable<(Component Owner, LipSyncSettings Value)> Enumerate(ExpressionComponent expression)
    {
        foreach (var (owner, incomingValue) in EnumerateIncoming(expression))
            yield return (owner, incomingValue);
        if (expression.HasLipSync && _sources.TryResolve(expression.LipSync, expression, out var value))
            yield return (expression, value);
    }

    public LipSyncSettings GetIncoming(Component target, out SettingsComponent? owner)
    {
        var value = new LipSyncSettings();
        owner = null;
        foreach (var (source, resolved) in EnumerateIncoming(target))
        {
            owner = source;
            value = resolved;
        }
        return value;
    }

    public LipSyncSettings Get(ExpressionComponent expression, out Component? owner)
    {
        var value = new LipSyncSettings();
        owner = null;
        foreach (var (source, resolved) in Enumerate(expression))
        {
            owner = source;
            value = resolved;
        }
        return value;
    }
}

internal sealed class FaceTuneTransitionResolver
{
    public IEnumerable<(SettingsComponent Owner, TransitionSettings Value)> EnumerateIncoming(Component target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);
        for (var i = settings.Length - 1; i >= 0; i--)
        {
            var owner = settings[i];
            if (owner.HasTransition)
                yield return (owner, owner.Transition);
        }
    }

    public IEnumerable<(Component Owner, TransitionSettings Value)> Enumerate(ExpressionComponent expression)
    {
        foreach (var (owner, incomingValue) in EnumerateIncoming(expression))
            yield return (owner, incomingValue);
        if (expression.HasTransition)
            yield return (expression, expression.Transition);
    }

    public TransitionSettings GetIncoming(Component target, out SettingsComponent? owner)
    {
        var value = new TransitionSettings();
        owner = null;
        foreach (var (source, resolved) in EnumerateIncoming(target))
        {
            owner = source;
            value = resolved;
        }
        return value;
    }

    public TransitionSettings Get(ExpressionComponent expression, out Component? owner)
    {
        var value = new TransitionSettings();
        owner = null;
        foreach (var (source, resolved) in Enumerate(expression))
        {
            owner = source;
            value = resolved;
        }
        return value;
    }
}

internal sealed class FaceTunePriorityResolver
{
    public IEnumerable<(SettingsComponent Owner, PrioritySettings Value)> EnumerateIncoming(Component target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);
        for (var i = settings.Length - 1; i >= 0; i--)
        {
            var owner = settings[i];
            if (owner.HasPriority)
                yield return (owner, owner.Priority);
        }
    }

    public IEnumerable<(Component Owner, PrioritySettings Value)> Enumerate(ExpressionComponent expression)
    {
        foreach (var (owner, incomingValue) in EnumerateIncoming(expression))
            yield return (owner, incomingValue);
        if (expression.HasPriority)
            yield return (expression, expression.Priority);
    }

    public PrioritySettings GetIncoming(Component target, out SettingsComponent? owner)
    {
        var value = new PrioritySettings();
        owner = null;
        foreach (var (source, resolved) in EnumerateIncoming(target))
        {
            owner = source;
            value = resolved;
        }
        return value;
    }

    public PrioritySettings Get(ExpressionComponent expression, out Component? owner)
    {
        var value = new PrioritySettings();
        owner = null;
        foreach (var (source, resolved) in Enumerate(expression))
        {
            owner = source;
            value = resolved;
        }
        return value;
    }
}

internal sealed class FaceTuneParameterDriverResolver
{
    private readonly FaceTuneSettingsSourceResolver _sources;

    internal FaceTuneParameterDriverResolver(FaceTuneSettingsSourceResolver sources) => _sources = sources;

    public IEnumerable<(SettingsComponent Owner, ParameterDriverSettings Value)> EnumerateIncoming(Component target)
    {
        var settings = target.GetComponentsInParentExcludingSelf<SettingsComponent>(true);
        for (var i = settings.Length - 1; i >= 0; i--)
        {
            var owner = settings[i];
            if (owner.HasParameterDriver && _sources.TryResolve(owner.ParameterDriver, owner, out var value))
                yield return (owner, value);
        }
    }

    public IEnumerable<(Component Owner, ParameterDriverSettings Value)> Enumerate(ExpressionComponent expression)
    {
        foreach (var (owner, incomingValue) in EnumerateIncoming(expression))
            yield return (owner, incomingValue);
        if (expression.HasParameterDriver && _sources.TryResolve(expression.ParameterDriver, expression, out var value))
            yield return (expression, value);
    }

    public ParameterDriverSettings GetIncoming(Component target)
    {
        var value = new ParameterDriverSettings();
        foreach (var (_, resolved) in EnumerateIncoming(target))
            value.Entries.AddRange(resolved.Entries);
        return value;
    }

    public ParameterDriverSettings Get(ExpressionComponent expression)
    {
        var value = new ParameterDriverSettings();
        foreach (var (_, resolved) in Enumerate(expression))
            value.Entries.AddRange(resolved.Entries);
        return value;
    }
}

internal sealed class FaceTuneSettingsSourceResolver
{
    public bool TryResolve<TValue>(
        ISettingsSource<TValue> source,
        Component owner,
        [NotNullWhen(true)] out TValue? value,
        Action<Component>? observe = null)
        where TValue : class
    {
        observe?.Invoke(owner);
        return TryResolve(source, new HashSet<Component> { owner }, observe, out value);
    }

    private static bool TryResolve<TValue>(
        ISettingsSource<TValue> source,
        HashSet<Component> visited,
        Action<Component>? observe,
        [NotNullWhen(true)] out TValue? value)
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
        ISettingsSource<TValue>? referencedValue = null;
        foreach (var component in source.Source.GetComponents<FaceTuneTagComponent>())
        {
            observe?.Invoke(component);
            if (component is not IReferenceableExpressionSettings<TValue> referenceable
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

        return TryResolve(referencedValue, visited, observe, out value);
    }
}

internal sealed class FaceTuneMenuResolver
{
    public IEnumerable<MenuComponent> EnumerateFolders(Component target)
    {
        var menus = target.GetComponentsInParentExcludingSelf<MenuComponent>(true);
        for (var i = menus.Length - 1; i >= 0; i--)
        {
            if (menus[i].MenuKind == MenuComponent.Kind.Folder)
                yield return menus[i];
        }
    }

    public Transform? GetInstallTarget(Component owner, MenuSettings menu)
    {
        if (menu.InstallContainer != null)
            return menu.InstallContainer;
        return EnumerateFolders(owner).LastOrDefault()?.transform;
    }
}

