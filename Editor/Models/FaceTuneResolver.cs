using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal sealed class FaceTuneResolver
{
    private readonly GameObject _root;
    private readonly ComputeContext? _context;
    private FaceTuneFacialDataResolver? _facialData;
    private FaceTuneExpressionDataResolver? _expressionData;
    private FaceTuneScopedResolver<EyeBlinkSettings>? _eyeBlink;
    private FaceTuneScopedResolver<LipSyncSettings>? _lipSync;
    private FaceTuneScopedResolver<TransitionSettings>? _transition;
    private FaceTuneScopedResolver<PrioritySettings>? _priority;
    private FaceTuneSettingsReferenceResolver? _settingsReferences;
    private FaceTuneMenuResolver? _menus;

    public FaceTuneFacialDataResolver FacialData
        => _facialData ??= new FaceTuneFacialDataResolver(
            _root,
            SettingsReferences,
            ExpressionData,
            _context);

    public FaceTuneExpressionDataResolver ExpressionData
        => _expressionData ??= new FaceTuneExpressionDataResolver(SettingsReferences, _context);

    public FaceTuneSettingsReferenceResolver SettingsReferences
        => _settingsReferences ??= new FaceTuneSettingsReferenceResolver();

    public FaceTuneScopedResolver<EyeBlinkSettings> EyeBlink => _eyeBlink ??= new(
        setting => SettingsReferences.TryResolve<EyeBlinkSettings>(setting, out var value) ? value : null,
        expression => SettingsReferences.TryResolve<EyeBlinkSettings>(expression, out var value) ? value : null,
        static () => new EyeBlinkSettings());

    public FaceTuneScopedResolver<LipSyncSettings> LipSync => _lipSync ??= new(
        setting => SettingsReferences.TryResolve<LipSyncSettings>(setting, out var value) ? value : null,
        expression => SettingsReferences.TryResolve<LipSyncSettings>(expression, out var value) ? value : null,
        static () => new LipSyncSettings());

    public FaceTuneScopedResolver<TransitionSettings> Transition => _transition ??= new(
        setting => setting.HasTransition ? setting.Transition : null,
        expression => expression.HasTransition ? expression.Transition : null,
        static () => new TransitionSettings());

    public FaceTuneScopedResolver<PrioritySettings> Priority => _priority ??= new(
        setting => setting.HasPriority ? setting.Priority : null,
        expression => expression.HasPriority ? expression.Priority : null,
        static () => new PrioritySettings());

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
    private readonly FaceTuneExpressionDataResolver _expressionData;
    private readonly ComputeContext _context;

    internal FaceTuneFacialDataResolver(
        GameObject root,
        FaceTuneSettingsReferenceResolver references,
        FaceTuneExpressionDataResolver expressionData,
        ComputeContext? context)
    {
        _root = root;
        _references = references;
        _expressionData = expressionData;
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
        => _expressionData.EnumerateLocal<FacialBlendShapeData>(expression);

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

internal sealed class FaceTuneExpressionDataResolver
{
    private readonly FaceTuneSettingsReferenceResolver _references;
    private readonly ComputeContext _context;

    internal FaceTuneExpressionDataResolver(
        FaceTuneSettingsReferenceResolver references,
        ComputeContext? context)
    {
        _references = references;
        _context = context ?? ComputeContext.NullContext;
    }

    public IEnumerable<(Component Owner, TValue Value)> EnumerateLocal<TValue>(
        ExpressionComponent expression)
        where TValue : class
    {
        var owner = _context.Observe(expression);
        if (_references.TryResolve<TValue>(owner, out var value, component => _context.Observe(component)))
            yield return (owner, value);

        foreach (var data in _context.GetComponentsInChildren<ExpressionDataComponent>(expression.gameObject, true))
        {
            var dataOwner = _context.Observe(data);
            if (_references.TryResolve<TValue>(dataOwner, out var dataValue, component => _context.Observe(component)))
                yield return (dataOwner, dataValue);
        }
    }
}

internal sealed class FaceTuneScopedResolver<TValue> where TValue : class
{
    private readonly Func<SettingsComponent, TValue?> _getSettings;
    private readonly Func<ExpressionComponent, TValue?> _getExpression;
    private readonly Func<TValue> _getDefault;

    internal FaceTuneScopedResolver(
        Func<SettingsComponent, TValue?> getSettings,
        Func<ExpressionComponent, TValue?> getExpression,
        Func<TValue> getDefault)
    {
        _getSettings = getSettings;
        _getExpression = getExpression;
        _getDefault = getDefault;
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
            value = resolved;
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
            value = resolved;
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

    public static string GetDisplayName(string? configuredName, string fallback)
        => string.IsNullOrWhiteSpace(configuredName) ? fallback : configuredName;

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

    public Transform? GetInstallTarget(Component owner, Transform? configured)
    {
        var validOwner = owner.DestroyedAsNull();
        if (validOwner == null || (validOwner.transform != _root && !validOwner.transform.IsChildOf(_root)))
            return null;
        configured = configured.DestroyedAsNull();
        if (configured != null && (configured == _root || configured.IsChildOf(_root)))
            return configured;
        if (configured != null)
            return null;
        var folder = EnumerateFolders(validOwner)
            .LastOrDefault()
            .DestroyedAsNull();
        return folder == null ? _root : folder.transform.DestroyedAsNull();
    }

    public void ValidateInstallTarget(Transform target, Component owner)
    {
        if (target != _root && !target.IsChildOf(_root))
        {
            throw new InvalidOperationException(
                $"Menu install target is outside the avatar: '{owner.name}'.");
        }

        if (target == owner.transform || target.IsChildOf(owner.transform))
        {
            throw new InvalidOperationException(
                $"Menu install target creates a hierarchy cycle: '{owner.name}'.");
        }
    }

    public Transform? ResolveDestination(
        MenuComponent menu,
        ISet<MenuComponent> localFolders,
        ISet<Transform> externalFolders)
    {
        for (var current = menu.transform.parent; current != null; current = current.parent)
        {
            var folder = current.GetComponent<MenuComponent>();
            if (folder != null && localFolders.Contains(folder))
                return folder.transform;
            if (externalFolders.Contains(current))
                return current;
        }

        return null;
    }

    public static Transform? ResolvePreviewTarget(Transform? explicitPreview, Component? owner)
    {
        var target = explicitPreview.DestroyedAsNull();
        var expressionOwner = (owner as ExpressionComponent).DestroyedAsNull();
        return target ?? expressionOwner?.transform.DestroyedAsNull();
    }

    public List<string> GetDefinedGroupNames()
        => _root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.MenuKind == MenuComponent.Kind.Toggle
                        && !menu.UseExistingParameter
                        && menu.GenerateParameterGroup
                        && !string.IsNullOrWhiteSpace(menu.GroupName))
            .Select(menu => menu.GroupName)
            .Concat(_root.GetComponentsInChildren<ExpressionComponent>(true)
                .Where(expression => expression.WriteMode == ExpressionWriteMode.Blend && expression.DirectMenuEnabled)
                .Select(expression => expression.DirectMenuSettings.GroupName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
}

