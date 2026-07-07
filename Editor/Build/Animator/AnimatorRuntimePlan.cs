using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal sealed record class ExpressionRuntimeModes(
    EyeBlinkRuntimeMode? EyeBlink,
    LipSyncRuntimeMode? LipSync);

internal sealed record class EyeBlinkRuntimeMode(EyeBlinkRuntimeModeKind Kind, AdvancedEyeBlinkSettings? AdvancedSettings = null)
{
    public static EyeBlinkRuntimeMode Tracking { get; } = new(EyeBlinkRuntimeModeKind.Tracking);
    public static EyeBlinkRuntimeMode Disabled { get; } = new(EyeBlinkRuntimeModeKind.Disabled);
    public static EyeBlinkRuntimeMode Advanced(AdvancedEyeBlinkSettings settings) => new(EyeBlinkRuntimeModeKind.Advanced, settings);
}

internal enum EyeBlinkRuntimeModeKind
{
    Tracking,
    Disabled,
    Advanced
}

internal sealed record class LipSyncRuntimeMode(LipSyncRuntimeModeKind Kind, AdvancedLipSyncSettings? Settings = null)
{
    public static LipSyncRuntimeMode Tracking { get; } = new(LipSyncRuntimeModeKind.Tracking);
    public static LipSyncRuntimeMode Disabled { get; } = new(LipSyncRuntimeModeKind.Disabled);
    public static LipSyncRuntimeMode Canceler(AdvancedLipSyncSettings settings) => new(LipSyncRuntimeModeKind.Canceler, settings);
}

internal enum LipSyncRuntimeModeKind
{
    Tracking,
    Disabled,
    Canceler
}

internal sealed class RuntimeDomainRegistry
{
    public RuntimeDomain<EyeBlinkRuntimeMode> EyeBlink { get; }
    public RuntimeDomain<LipSyncRuntimeMode> LipSync { get; }

    private RuntimeDomainRegistry(
        RuntimeDomain<EyeBlinkRuntimeMode> eyeBlink,
        RuntimeDomain<LipSyncRuntimeMode> lipSync)
    {
        EyeBlink = eyeBlink;
        LipSync = lipSync;
    }

    public static RuntimeDomainRegistry Create(IEnumerable<OutputUnit> units)
    {
        var unitList = units.ToArray();
        return new RuntimeDomainRegistry(
            RuntimeDomain<EyeBlinkRuntimeMode>.Create(
                $"{FaceTuneConstants.ParameterPrefix}/Blink/Mode",
                EyeBlinkRuntimeMode.Tracking,
                unitList.SelectMany(unit => unit.Items.Select(item => (unit, item.RuntimeModes.EyeBlink)))),
            RuntimeDomain<LipSyncRuntimeMode>.Create(
                $"{FaceTuneConstants.ParameterPrefix}/LipSync/Mode",
                LipSyncRuntimeMode.Tracking,
                unitList.SelectMany(unit => unit.Items.Select(item => (unit, item.RuntimeModes.LipSync)))));
    }

    public void EnsureParameters(VirtualAnimatorController controller)
    {
        EyeBlink.EnsureParameter(controller);
        LipSync.EnsureParameter(controller);
    }

    public void AddModeCurves(VirtualClip clip, OutputUnit unit, AnimatorExpressionItem item)
    {
        EyeBlink.AddModeCurve(clip, unit, item.RuntimeModes.EyeBlink);
        LipSync.AddModeCurve(clip, unit, item.RuntimeModes.LipSync);
    }
}

internal sealed class RuntimeDomain<TMode> where TMode : class
{
    private readonly Dictionary<(int UnitId, TMode Mode), ModeEntry<TMode>> _localEntries;

    public string ParameterName { get; }
    public ModeEntry<TMode> Baseline { get; }
    public IReadOnlyList<ModeEntry<TMode>> Entries { get; }

    private RuntimeDomain(string parameterName, ModeEntry<TMode> baseline, IReadOnlyList<ModeEntry<TMode>> entries)
    {
        ParameterName = parameterName;
        Baseline = baseline;
        Entries = entries;
        _localEntries = entries
            .Where(entry => entry.Unit != null)
            .ToDictionary(entry => (entry.Unit!.Id, entry.Mode));
    }

    public static RuntimeDomain<TMode> Create(
        string parameterName,
        TMode baselineMode,
        IEnumerable<(OutputUnit unit, TMode? mode)> unitModes)
    {
        var entries = new List<ModeEntry<TMode>>();
        var baseline = new ModeEntry<TMode>(null, baselineMode, 0);
        entries.Add(baseline);

        foreach (var group in unitModes
            .Where(entry => entry.mode != null)
            .GroupBy(entry => (entry.unit.Id, entry.mode)))
        {
            if (entries.Count > 255)
            {
                throw new InvalidOperationException($"Too many FaceTune runtime modes for '{parameterName}'.");
            }

            entries.Add(new ModeEntry<TMode>(group.First().unit, group.First().mode!, entries.Count));
        }

        return new RuntimeDomain<TMode>(parameterName, baseline, entries);
    }

    public IEnumerable<ModeEntry<TMode>> LocalEntries(OutputUnit unit)
    {
        return Entries.Where(entry => entry.Unit == unit);
    }

    public IEnumerable<ModeEntry<TMode>> ForeignEntries(OutputUnit unit)
    {
        return Entries.Where(entry => entry.Unit != null && entry.Unit != unit);
    }

    public ModeEntry<TMode> EntryFor(OutputUnit unit, TMode mode)
    {
        return _localEntries[(unit.Id, mode)];
    }

    public void EnsureParameter(VirtualAnimatorController controller)
    {
        if (Entries.Count > 1)
        {
            controller.EnsureFloatParameterExists(ParameterName);
        }
    }

    public void AddModeCurve(VirtualClip clip, OutputUnit unit, TMode? mode)
    {
        if (mode == null) return;

        var curve = new AnimationCurve();
        curve.AddKey(0f, EntryFor(unit, mode).Value);
        clip.SetFloatCurve("", typeof(UnityEngine.Animator), ParameterName, curve);
    }
}

internal sealed record class ModeEntry<TMode>(OutputUnit? Unit, TMode Mode, int Index) where TMode : class
{
    public float Value => VRCAAPHelper.IndexToValue(Index);
}
