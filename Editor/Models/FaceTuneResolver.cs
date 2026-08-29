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
        => _settingsReferences ??= new FaceTuneSettingsReferenceResolver(_context);

    public FaceTuneScopedResolver<EyeBlinkSettings> EyeBlink => _eyeBlink ??= new(
        _root,
        setting => SettingsReferences.TryResolve<EyeBlinkSettings>(setting, out var value) ? value : null,
        expression => SettingsReferences.TryResolve<EyeBlinkSettings>(expression, out var value) ? value : null,
        static () => new EyeBlinkSettings());

    public FaceTuneScopedResolver<LipSyncSettings> LipSync => _lipSync ??= new(
        _root,
        setting => SettingsReferences.TryResolve<LipSyncSettings>(setting, out var value) ? value : null,
        expression => SettingsReferences.TryResolve<LipSyncSettings>(expression, out var value) ? value : null,
        static () => new LipSyncSettings());

    public FaceTuneScopedResolver<TransitionSettings> Transition => _transition ??= new(
        _root,
        setting => setting.HasTransition ? setting.Transition : null,
        expression => expression.HasTransition ? expression.Transition : null,
        static () => new TransitionSettings());

    public FaceTuneScopedResolver<PrioritySettings> Priority => _priority ??= new(
        _root,
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
        foreach (var owner in _context.GetComponentsInParentExcludingSelf<SettingsComponent>(_root, target, true))
        {
            if (_references.TryResolve(
                    owner,
                    ExtractFacialSettings,
                    out FacialBlendShapeData? value))
                yield return (owner, value);
        }
    }

    public IEnumerable<(SettingsComponent Owner, FacialBlendShapeData Value)> EnumerateIncoming(GameObject target)
        => EnumerateIncoming(target.transform);

    public IEnumerable<(Component Owner, FacialBlendShapeData Value)> EnumerateLocal(ExpressionComponent expression)
        => _expressionData.EnumerateLocal(expression, ExtractFacialSettings);

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

    public void AddIncoming(GameObject target, ICollection<BlendShapeWeightAnimation> result, string bodyPath)
        => AddIncoming(target.transform, result, bodyPath);

    public void AddLocal(ExpressionComponent expression, ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        foreach (var (_, value) in EnumerateLocal(expression))
            AddAnimations(value, result, bodyPath);
    }

    public bool AddLocalData(
        GameObject scope,
        ICollection<BlendShapeWeightAnimation> result,
        string bodyPath)
    {
        var added = false;
        foreach (var (_, value) in _expressionData.EnumerateData(scope, ExtractFacialSettings))
        {
            added = true;
            AddAnimations(value, result, bodyPath);
        }
        return added;
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
            var owner = settings;
            var applyToRenderer = _context.Observe(
                owner,
                value => value.ApplyToRenderer,
                (left, right) => left == right);
            if (applyToRenderer
                && _references.TryResolve(
                    owner,
                    ExtractFacialSettings,
                    out FacialBlendShapeData? value))
                AddAnimations(value, result, bodyPath);
        }
    }

    private ReferenceableExpressionSettings<FacialBlendShapeData> ExtractFacialSettings(Component component)
    {
        return component switch
        {
            SettingsComponent settings => ExtractFacialSettings(
                settings,
                value => value.HasFacialBlendShapes,
                value => value.FacialBlendShapesReference,
                value => value.FacialBlendShapes),
            ExpressionComponent expression => ExtractFacialSettings(
                expression,
                static _ => true,
                value => value.FacialBlendShapesReference,
                value => value.FacialBlendShapes),
            ExpressionDataComponent data => ExtractFacialSettings(
                data,
                static _ => true,
                value => value.FacialBlendShapesReference,
                value => value.FacialBlendShapes),
            _ => throw new ArgumentException(
                $"Component does not provide facial data: {component.GetType().Name}",
                nameof(component))
        };
    }

    private ReferenceableExpressionSettings<FacialBlendShapeData> ExtractFacialSettings<TComponent>(
        TComponent owner,
        Func<TComponent, bool> getEnabled,
        Func<TComponent, SettingsReference> getReference,
        Func<TComponent, FacialBlendShapeData> getData)
        where TComponent : Component
    {
        var settings = _context.Observe(
            owner,
            component => new ReferenceableExpressionSettings<FacialBlendShapeData>(
                getEnabled(component),
                getReference(component).Mode,
                getReference(component).Source,
                getData(component).Clone()),
            (left, right) => left.Equals(right));
        return settings;
    }

    private void AddAnimations(FacialBlendShapeData data, ICollection<BlendShapeWeightAnimation> result, string bodyPath)
    {
        if (data.Clip != null)
        {
            var clip = _context.Observe(data.Clip);
            clip.GetBlendShapeAnimations(data.ClipOption, result, bodyPath);
        }

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
        => EnumerateLocal(
            expression,
            static component => ((IReferenceableExpressionSettings<TValue>)component).Settings);

    public IEnumerable<(Component Owner, TValue Value)> EnumerateLocal<TValue>(
        ExpressionComponent expression,
        Func<Component, ReferenceableExpressionSettings<TValue>> extract)
        where TValue : class
    {
        if (_references.TryResolve<TValue>(expression, extract, out var value))
            yield return (expression, value);

        foreach (var item in EnumerateData(expression.gameObject, extract))
            yield return item;
    }

    public IEnumerable<(Component Owner, TValue Value)> EnumerateData<TValue>(
        GameObject scope,
        Func<Component, ReferenceableExpressionSettings<TValue>> extract)
        where TValue : class
    {
        foreach (var data in _context.GetComponentsInChildren<ExpressionDataComponent>(scope, true))
        {
            if (_references.TryResolve<TValue>(data, extract, out var dataValue))
                yield return (data, dataValue);
        }
    }
}

internal sealed class FaceTuneScopedResolver<TValue> where TValue : class
{
    private readonly GameObject _root;
    private readonly Func<SettingsComponent, TValue?> _getSettings;
    private readonly Func<ExpressionComponent, TValue?> _getExpression;
    private readonly Func<TValue> _getDefault;

    internal FaceTuneScopedResolver(
        GameObject root,
        Func<SettingsComponent, TValue?> getSettings,
        Func<ExpressionComponent, TValue?> getExpression,
        Func<TValue> getDefault)
    {
        _root = root;
        _getSettings = getSettings;
        _getExpression = getExpression;
        _getDefault = getDefault;
    }

    public IEnumerable<(SettingsComponent Owner, TValue Value)> EnumerateIncoming(Component target)
    {
        foreach (var owner in _root.GetComponentsInParentExcludingSelf<SettingsComponent>(target, true))
        {
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
    private readonly ComputeContext _context;

    internal FaceTuneSettingsReferenceResolver(ComputeContext? context = null)
    {
        _context = context ?? ComputeContext.NullContext;
    }

    public bool TryResolve<TValue>(
        Component owner,
        [NotNullWhen(true)] out TValue? value)
        where TValue : class
        => TryResolve(owner, GetSettings<TValue>, out value);

    public bool TryResolve<TValue>(
        Component owner,
        Func<Component, ReferenceableExpressionSettings<TValue>> extract,
        [NotNullWhen(true)] out TValue? value)
        where TValue : class
        => TryResolve(owner, new HashSet<Component>(), extract, out value);

    private bool TryResolve<TValue>(
        Component owner,
        HashSet<Component> visited,
        Func<Component, ReferenceableExpressionSettings<TValue>> extract,
        [NotNullWhen(true)] out TValue? value)
        where TValue : class
    {
        if (!visited.Add(owner)
            || owner is not IReferenceableExpressionSettings<TValue>)
        {
            value = null;
            return false;
        }

        var source = extract(owner);
        if (!source.Enabled)
        {
            value = null;
            return false;
        }

        return TryResolve(source, visited, extract, out value);
    }

    private bool TryResolve<TValue>(
        ReferenceableExpressionSettings<TValue> source,
        HashSet<Component> visited,
        Func<Component, ReferenceableExpressionSettings<TValue>> extract,
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
        foreach (var component in _context.GetComponents<FaceTuneTagComponent>(source.Source.gameObject))
        {
            if (component is not IReferenceableExpressionSettings<TValue>)
                continue;

            var candidate = extract(component);
            if (!candidate.Enabled)
                continue;

            // 同じGameObjectではInspector上で下にあるComponentを使う。
            referencedOwner = component;
            referencedValue = candidate;
        }

        if (referencedOwner == null
            || referencedValue == null
            || !visited.Add(referencedOwner))
        {
            value = null;
            return false;
        }

        return TryResolve(referencedValue.Value, visited, extract, out value);
    }

    private static ReferenceableExpressionSettings<TValue> GetSettings<TValue>(Component component)
        where TValue : class
        => ((IReferenceableExpressionSettings<TValue>)component).Settings;
}

internal sealed class FaceTuneMenuResolver
{
    private readonly Transform _root;
    private readonly HashSet<Transform> _localFolders;
    private readonly HashSet<Transform> _externalFolders;

    internal FaceTuneMenuResolver(
        GameObject root,
        IEnumerable<Transform>? externalFolders = null)
    {
        _root = root.transform;
        _localFolders = root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.MenuKind == MenuComponent.Kind.Folder)
            .Select(menu => menu.transform)
            .ToHashSet();
        _externalFolders = (externalFolders ?? Array.Empty<Transform>()).ToHashSet();
    }

    public Transform Root => _root;

    public static string GetDisplayName(string? configuredName, string fallback)
        => string.IsNullOrWhiteSpace(configuredName) ? fallback : configuredName!;

    public Transform? ResolveDestination(Component owner, Transform? configuredTarget)
    {
        var validOwner = owner.DestroyedAsNull();
        if (validOwner == null || !IsInRoot(validOwner.transform))
            return null;

        var configured = configuredTarget.DestroyedAsNull();
        if (configured != null && !IsInRoot(configured))
            return null;

        IEnumerable<Transform> candidates = configured is { } target
            ? new[] { target }.Concat(
                _root.gameObject
                    .GetComponentsInParentExcludingSelf<Transform>(target, true)
                    .Reverse())
            : _root.gameObject
                .GetComponentsInParentExcludingSelf<Transform>(validOwner, true)
                .Reverse();
        return candidates.FirstOrDefault(IsDestination) ?? _root;
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

    private bool IsDestination(Transform target)
        => _localFolders.Contains(target) || _externalFolders.Contains(target);

    private bool IsInRoot(Transform target)
        => target == _root || target.IsChildOf(_root);

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

