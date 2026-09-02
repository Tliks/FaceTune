using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal readonly record struct ScopedValue<T>(T Value, SettingsComponent? Owner);
internal readonly record struct ExpressionBehavior(ExpressionWriteMode WriteMode, TrackingPermission AllowEyeBlink, TrackingPermission AllowLipSync);

internal sealed class ResolvedExpression
{
    private readonly ExpressionResolver resolver;
    private BlendShapeWeightAnimationSet? incomingFacial, definitionFacial, facial;
    private ResolvedNonFacialAnimationSet? nonFacial;
    private ExpressionBehavior? behavior;
    private MultiFrameSettings? multiFrame;
    private EyeBlinkSettings? definitionEyeBlink, eyeBlink;
    private LipSyncSettings? definitionLipSync, lipSync;
    private TransitionSettings? transition;
    private PrioritySettings? priority;
    private ScopedValue<EyeBlinkSettings>? inheritedEyeBlink;
    private ScopedValue<LipSyncSettings>? inheritedLipSync;
    private ScopedValue<TransitionSettings>? inheritedTransition;
    private ScopedValue<PrioritySettings>? inheritedPriority;
    private bool behaviorResolved, eyeBlinkResolved, lipSyncResolved;

    internal ResolvedExpression(
        ExpressionResolver resolver,
        ExpressionComponent expression,
        Component? source,
        string bodyPath)
    {
        this.resolver = resolver;
        Expression = expression;
        DefinitionSource = source;
        BodyPath = bodyPath;
    }

    public ExpressionComponent Expression { get; }
    public Component? DefinitionSource { get; }
    public string BodyPath { get; }
    public BlendShapeWeightAnimationSet IncomingFacial => incomingFacial ??= resolver.Facial.ResolveIncoming(Expression.transform, BodyPath);
    public BlendShapeWeightAnimationSet DefinitionFacial => definitionFacial ??= resolver.ResolveDefinitionFacial(DefinitionSource, BodyPath);
    public BlendShapeWeightAnimationSet Facial
    {
        get
        {
            if (facial != null) return facial;
            facial = IncomingFacial.Clone();
            facial.AddRange(DefinitionFacial);
            return facial;
        }
    }
    public ResolvedNonFacialAnimationSet NonFacial => nonFacial ??= resolver.NonFacial.ResolveDefinition(DefinitionSource, BodyPath);
    private ExpressionBehavior? Behavior
    {
        get
        {
            if (!behaviorResolved)
            {
                behavior = resolver.ResolveBehavior(DefinitionSource);
                behaviorResolved = true;
            }
            return behavior;
        }
    }
    public ExpressionWriteMode WriteMode => Behavior?.WriteMode ?? ExpressionComponent.DefaultWriteMode;
    public MultiFrameSettings MultiFrame => multiFrame ??= resolver.ResolveMultiFrame(DefinitionSource) ?? new MultiFrameSettings();
    public TrackingPermission AllowEyeBlink => Behavior?.AllowEyeBlink ?? ExpressionComponent.DefaultAllowEyeBlink;
    public TrackingPermission AllowLipSync => Behavior?.AllowLipSync ?? ExpressionComponent.DefaultAllowLipSync;

    public EyeBlinkSettings? DefinitionEyeBlink
    {
        get
        {
            if (!eyeBlinkResolved)
            {
                definitionEyeBlink = resolver.ResolveDefinitionEyeBlink(DefinitionSource);
                eyeBlinkResolved = true;
            }
            return definitionEyeBlink;
        }
    }
    public ScopedValue<EyeBlinkSettings> InheritedEyeBlink
        => inheritedEyeBlink ??= resolver.EyeBlinkScope.GetIncoming(Expression);
    public EyeBlinkSettings EyeBlink => eyeBlink ??= DefinitionEyeBlink ?? InheritedEyeBlink.Value;

    public LipSyncSettings? DefinitionLipSync
    {
        get
        {
            if (!lipSyncResolved)
            {
                definitionLipSync = resolver.ResolveDefinitionLipSync(DefinitionSource);
                lipSyncResolved = true;
            }
            return definitionLipSync;
        }
    }
    public ScopedValue<LipSyncSettings> InheritedLipSync
        => inheritedLipSync ??= resolver.LipSyncScope.GetIncoming(Expression);
    public LipSyncSettings LipSync => lipSync ??= DefinitionLipSync ?? InheritedLipSync.Value;
    public ScopedValue<TransitionSettings> InheritedTransition
        => inheritedTransition ??= resolver.TransitionScope.GetIncoming(Expression);
    public TransitionSettings Transition => transition ??= resolver.ResolveTransition(Expression, InheritedTransition.Value);
    public ScopedValue<PrioritySettings> InheritedPriority
        => inheritedPriority ??= resolver.PriorityScope.GetIncoming(Expression);
    public PrioritySettings Priority => priority ??= resolver.ResolvePriority(Expression, InheritedPriority.Value);
}

internal sealed class ExpressionResolver
{
    private readonly ComputeContext context;
    private readonly ExpressionDefinitionSourceResolver sources;
    private readonly ReferenceableSettingResolver<EyeBlinkSettings> eyeBlink;
    private readonly ReferenceableSettingResolver<LipSyncSettings> lipSync;

    internal FacialAnimationResolver Facial { get; }
    internal NonFacialAnimationResolver NonFacial { get; }
    internal ScopedValueResolver<EyeBlinkSettings> EyeBlinkScope { get; }
    internal ScopedValueResolver<LipSyncSettings> LipSyncScope { get; }
    internal ScopedValueResolver<TransitionSettings> TransitionScope { get; }
    internal ScopedValueResolver<PrioritySettings> PriorityScope { get; }

    public ExpressionResolver(GameObject root, ComputeContext? context = null)
    {
        this.context = context ?? ComputeContext.NullContext;
        sources = new ExpressionDefinitionSourceResolver(context);
        Facial = new FacialAnimationResolver(root, context);
        NonFacial = new NonFacialAnimationResolver(root, context);
        eyeBlink = new ReferenceableSettingResolver<EyeBlinkSettings>(value => value.Clone(), context);
        lipSync = new ReferenceableSettingResolver<LipSyncSettings>(value => value.Clone(), context);
        EyeBlinkScope = new ScopedValueResolver<EyeBlinkSettings>(root, settings => eyeBlink.Resolve(settings), static () => new EyeBlinkSettings(), context);
        LipSyncScope = new ScopedValueResolver<LipSyncSettings>(root, settings => lipSync.Resolve(settings), static () => new LipSyncSettings(), context);
        TransitionScope = new ScopedValueResolver<TransitionSettings>(root, ReadTransition, static () => new TransitionSettings(), context);
        PriorityScope = new ScopedValueResolver<PrioritySettings>(root, ReadPriority, static () => new PrioritySettings(), context);
    }

    public ResolvedExpression Resolve(ExpressionComponent expression, string bodyPath)
        => new(this, expression, sources.Find(expression), bodyPath);

    internal BlendShapeWeightAnimationSet ResolveDefinitionFacial(Component? source, string bodyPath)
        => source != null && Facial.TryResolve(source, bodyPath, out var value) ? value : new BlendShapeWeightAnimationSet();

    internal ExpressionBehavior? ResolveBehavior(Component? source)
        => source switch
        {
            ExpressionComponent expression => context.Observe(
                expression,
                value => new ExpressionBehavior(
                    value.WriteMode,
                    value.AllowEyeBlink,
                    value.AllowLipSync),
                (a, b) => a == b),
            ExpressionDataComponent data => ReadBehavior(data),
            _ => null
        };

    private ExpressionBehavior? ReadBehavior(ExpressionDataComponent owner)
    {
        var value = context.Observe(
            owner,
            current => (
                current.HasFacialBehavior,
                new ExpressionBehavior(
                    current.WriteMode,
                    current.AllowEyeBlink,
                    current.AllowLipSync)),
            (a, b) => a == b);
        return value.HasFacialBehavior ? value.Item2 : null;
    }

    internal MultiFrameSettings? ResolveMultiFrame(Component? source)
        => source switch
        {
            ExpressionComponent expression => ObserveMultiFrame(expression, value => value.MultiFrame),
            ExpressionDataComponent data when context.Observe(data, value => value.HasMultiFrame, (a, b) => a == b) => ObserveMultiFrame(data, value => value.MultiFrame),
            _ => null
        };

    internal EyeBlinkSettings? ResolveDefinitionEyeBlink(Component? source) => source == null ? null : eyeBlink.Resolve(source);
    internal LipSyncSettings? ResolveDefinitionLipSync(Component? source) => source == null ? null : lipSync.Resolve(source);
    internal TransitionSettings ResolveTransition(ExpressionComponent expression, TransitionSettings inherited) => ReadTransition(expression) ?? inherited;
    internal PrioritySettings ResolvePriority(ExpressionComponent expression, PrioritySettings inherited) => ReadPriority(expression) ?? inherited;

    private MultiFrameSettings ObserveMultiFrame<T>(T owner, Func<T, MultiFrameSettings> getValue) where T : Component
        => context.Observe(owner, value => getValue(value).Clone(), (a, b) => a.Equals(b));

    private TransitionSettings? ReadTransition(SettingsComponent owner)
    {
        var value = context.Observe(
            owner,
            current => (current.HasTransition, current.Transition.DurationSeconds),
            (a, b) => a == b);
        return value.HasTransition
            ? new TransitionSettings { DurationSeconds = value.DurationSeconds }
            : null;
    }

    private TransitionSettings? ReadTransition(ExpressionComponent owner)
    {
        var value = context.Observe(
            owner,
            current => (current.HasTransition, current.Transition.DurationSeconds),
            (a, b) => a == b);
        return value.HasTransition
            ? new TransitionSettings { DurationSeconds = value.DurationSeconds }
            : null;
    }

    private PrioritySettings? ReadPriority(SettingsComponent owner)
    {
        var value = context.Observe(
            owner,
            current => (current.HasPriority, current.Priority.Priority),
            (a, b) => a == b);
        return value.HasPriority ? new PrioritySettings { Priority = value.Priority } : null;
    }

    private PrioritySettings? ReadPriority(ExpressionComponent owner)
    {
        var value = context.Observe(
            owner,
            current => (current.HasPriority, current.Priority.Priority),
            (a, b) => a == b);
        return value.HasPriority ? new PrioritySettings { Priority = value.Priority } : null;
    }
}

internal sealed class ScopedValueResolver<T> where T : class
{
    private readonly GameObject root;
    private readonly Func<SettingsComponent, T?> getSettings;
    private readonly Func<T> getDefault;
    private readonly ComputeContext context;

    public ScopedValueResolver(
        GameObject root,
        Func<SettingsComponent, T?> getSettings,
        Func<T> getDefault,
        ComputeContext? context)
    {
        this.root = root;
        this.getSettings = getSettings;
        this.getDefault = getDefault;
        this.context = context ?? ComputeContext.NullContext;
    }

    public ScopedValue<T> GetIncoming(Component target)
    {
        var value = getDefault();
        SettingsComponent? owner = null;
        foreach (var settings in context.GetComponentsInParentExcludingSelf<SettingsComponent>(root, target, true))
        {
            if (getSettings(settings) is not { } resolved) continue;
            value = resolved;
            owner = settings;
        }
        return new ScopedValue<T>(value, owner);
    }
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
    {
        var expressions = new ExpressionResolver(_root.gameObject);
        return _root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.MenuKind == MenuComponent.Kind.Toggle
                        && !menu.UseExistingParameter
                        && menu.GenerateParameterGroup
                        && !string.IsNullOrWhiteSpace(menu.GroupName))
            .Select(menu => menu.GroupName)
            .Concat(_root.GetComponentsInChildren<ExpressionComponent>(true)
                .Where(expression => expressions.Resolve(expression, string.Empty).WriteMode
                                     == ExpressionWriteMode.Blend
                                  && expression.DirectMenuEnabled)
                .Select(expression => expression.DirectMenuSettings.GroupName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}

