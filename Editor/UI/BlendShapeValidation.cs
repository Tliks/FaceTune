namespace Aoyon.FaceTune.Gui;

internal enum BlendShapeValidationIssue
{
    None,
    Missing,
    Unavailable
}

internal sealed class BlendShapeValidationData
{
    private readonly ImmutableHashSet<string> _existingNames;
    private readonly IReadOnlyDictionary<FaceTuneWriteKind, ImmutableHashSet<string>> _unavailableNames;

    private BlendShapeValidationData(
        ImmutableHashSet<string> existingNames,
        IReadOnlyDictionary<FaceTuneWriteKind, ImmutableHashSet<string>> unavailableNames)
    {
        _existingNames = existingNames;
        _unavailableNames = unavailableNames;
    }

    internal static BlendShapeValidationData? Create(SerializedObject serializedObject)
    {
        if (serializedObject.targetObjects.Length != 1
            || SerializedObjectGUIContext.GetComponent(serializedObject) is not { } component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _))
            return null;

        var existingNames = Enumerable.Range(0, avatar.FaceMesh.blendShapeCount)
            .Select(avatar.FaceMesh.GetBlendShapeName)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var unavailableNames = Enum.GetValues(typeof(FaceTuneWriteKind))
            .Cast<FaceTuneWriteKind>()
            .ToDictionary(
                kind => kind,
                kind => AvatarContext.GetUnavailableBlendShapeNames(avatar.Root, kind));
        return new BlendShapeValidationData(existingNames, unavailableNames);
    }

    internal BlendShapeValidationIssue Validate(string name, FaceTuneWriteKind? writeKind)
    {
        if (string.IsNullOrWhiteSpace(name)) return BlendShapeValidationIssue.None;
        if (!_existingNames.Contains(name)) return BlendShapeValidationIssue.Missing;
        return writeKind is { } kind
               && _unavailableNames.TryGetValue(kind, out var unavailable)
               && unavailable.Contains(name)
            ? BlendShapeValidationIssue.Unavailable
            : BlendShapeValidationIssue.None;
    }
}

internal static class BlendShapeValidationScope
{
    [ThreadStatic] private static State? _current;

    internal static IDisposable Push(
        BlendShapeValidationData? data,
        FaceTuneWriteKind? writeKind = null)
    {
        var previous = _current;
        _current = new State(data, writeKind);
        return new Scope(previous);
    }

    internal static IDisposable PushWriteKind(FaceTuneWriteKind writeKind)
    {
        var previous = _current;
        _current = previous == null
            ? null
            : previous with { WriteKind = writeKind };
        return new Scope(previous);
    }

    internal static BlendShapeValidationIssue Validate(string name)
        => _current?.Data?.Validate(name, _current.WriteKind)
           ?? BlendShapeValidationIssue.None;

    private sealed record State(
        BlendShapeValidationData? Data,
        FaceTuneWriteKind? WriteKind);

    private sealed class Scope : IDisposable
    {
        private readonly State? _previous;
        private bool _disposed;

        internal Scope(State? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _current = _previous;
        }
    }
}
