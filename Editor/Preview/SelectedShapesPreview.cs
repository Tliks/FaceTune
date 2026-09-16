using Aoyon.FaceTune.Settings;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal enum VisemeHoverSource
{
    Overlay,
    Inspector
}

internal sealed class SelectedShapesPreviewSession : IDisposable
{
    private readonly ComputeContext _context;
    private readonly Action _onInvalidate;
    private bool _disposed;

    private SelectedShapesPreviewSession(Action onInvalidate)
    {
        _onInvalidate = onInvalidate;
        _context = new($"{nameof(SelectedShapesPreviewSession)}:{nameof(_context)}");
        _context.InvokeOnInvalidate(this, session => session.OnInvalidate());
    }

    internal SelectedPreviewData Data { get; private set; } = null!;

    internal static SelectedShapesPreviewSession? Create(
        Object selection,
        DirectBlendShapePreviewLayer preview,
        Action onInvalidate)
    {
        var session = new SelectedShapesPreviewSession(onInvalidate);
        var data = SelectedPreviewResolver.Resolve(selection, preview, session._context);
        if (data == null)
        {
            session.Dispose();
            return null;
        }
        session.Data = data;
        return session;
    }

    private void OnInvalidate()
    {
        if (!_disposed) _onInvalidate();
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

internal sealed class SelectedShapesPreview
{
    private readonly DirectBlendShapePreviewLayer _expressionLayer;
    private readonly DirectBlendShapePreviewLayer _eyeBlinkLayer;
    private readonly DirectBlendShapePreviewLayer _lipSyncCancellerLayer;
    private readonly DirectBlendShapePreviewLayer _lipSyncVisemeLayer;
    private readonly PreviewTimeline _multiFrame = new();
    private readonly PreviewTimeline _eyeBlink = new();

    private Object? _selection;
    private SelectedShapesPreviewSession? _session;
    private AvatarPreviewData? _currentAvatar;
    private bool _eyeBlinkActive;
    private int _selectedViseme = -1;
    private int _hoveredViseme = -1;
    private VisemeHoverSource? _visemeHoverSource;
    private int _suspendDepth;

    internal SelectedShapesPreview(
        DirectBlendShapePreviewLayer expressionLayer,
        DirectBlendShapePreviewLayer eyeBlinkLayer,
        DirectBlendShapePreviewLayer lipSyncCancellerLayer,
        DirectBlendShapePreviewLayer lipSyncVisemeLayer)
    {
        _expressionLayer = expressionLayer;
        _eyeBlinkLayer = eyeBlinkLayer;
        _lipSyncCancellerLayer = lipSyncCancellerLayer;
        _lipSyncVisemeLayer = lipSyncVisemeLayer;

        _multiFrame.TimeChanged += _ => ApplyFacial();
        _multiFrame.StateChanged += RepaintOverlay;
        _eyeBlink.TimeChanged += _ => ApplyEyeBlink();
        _eyeBlink.Completed += OnEyeBlinkCompleted;
        _eyeBlink.StateChanged += RepaintOverlay;
        ProjectSettings.SelectedExpressionPreviewSettingsChanged += RebuildFromSelection;
        Selection.selectionChanged += OnSelectionChanged;
        OnSelectionChanged();
    }

    private SelectedPreviewData? Data => _session?.Data;

    private AvatarPreviewData? CurrentAvatar => _currentAvatar;

    internal event Action? TargetsChanged;

    internal int AvatarCount => Data?.Avatars.Count ?? 0;
    internal GameObject? CurrentAvatarRoot => CurrentAvatar?.Root;
    internal int SelectedAvatarIndex
    {
        get
        {
            var current = CurrentAvatar;
            var avatars = Data?.Avatars;
            if (current == null || avatars == null) return -1;
            for (var index = 0; index < avatars.Count; index++)
            {
                if (ReferenceEquals(avatars[index], current)) return index;
            }
            return -1;
        }
    }

    internal bool HasMultiFrame => CurrentAvatar?.Facial?.MultiFrame != null;
    internal bool IsMultiFramePlaying => HasMultiFrame && _multiFrame.IsPlaying;
    internal float MultiFrameTime => _multiFrame.NormalizedTime;
    internal TrackingBehaviorDisplay EyeBlinkBehavior
        => CurrentAvatar?.EyeBlinkBehavior ?? TrackingBehaviorDisplay.NotApplicable;
    internal TrackingSettingDisplay EyeBlinkSetting
        => CurrentAvatar?.EyeBlinkSetting ?? TrackingSettingDisplay.Hidden;
    internal bool CanPreviewEyeBlink => CurrentAvatar?.EyeBlink != null;
    internal bool IsEyeBlinkPreviewApplied => _eyeBlinkActive;
    internal bool IsEyeBlinkPlaying => CanPreviewEyeBlink && _eyeBlink.IsPlaying;
    internal float EyeBlinkTime => _eyeBlink.NormalizedTime;
    internal Vector2? EyeBlinkClosedRange => CurrentAvatar?.EyeBlink?.ClosedRange;
    internal TrackingBehaviorDisplay LipSyncBehavior
        => CurrentAvatar?.LipSyncBehavior ?? TrackingBehaviorDisplay.NotApplicable;
    internal TrackingSettingDisplay LipSyncSetting
        => CurrentAvatar?.LipSyncSetting ?? TrackingSettingDisplay.Hidden;
    internal bool CanPreviewLipSync => CurrentAvatar?.LipSync != null;
    internal int SelectedViseme => _selectedViseme;
    internal Object? CurrentSource
        => (Object?)CurrentAvatar?.Source ?? (_selection as AnimationClip);

    internal string GetAvatarName(int index)
        => Data?.Avatars[index].Root.name ?? string.Empty;

    internal void Suspend()
    {
        _suspendDepth++;
        if (_suspendDepth != 1) return;

        DisposeSession();
        NotifyTargetsChanged();
    }

    internal void Resume()
    {
        if (_suspendDepth == 0) return;
        _suspendDepth--;
        if (_suspendDepth != 0) return;

        RebuildSession(resetControls: true);
    }

    internal void SetAvatar(int index)
    {
        var avatars = Data?.Avatars;
        if (avatars == null || index < 0 || index >= avatars.Count) return;
        var next = avatars[index];
        if (ReferenceEquals(next, CurrentAvatar)) return;

        ClearCurrentPreview();
        _currentAvatar = next;
        ConfigureTimelines(restartMultiFrame: false);
        if (!CanPreviewEyeBlink) _eyeBlinkActive = false;
        if (!CanPreviewLipSync) ResetVisemes();
        ApplyAll();
        RepaintOverlay();
    }

    internal void ToggleMultiFramePlayback()
        => _multiFrame.TogglePlayback();

    internal void SetMultiFrameTime(float value)
        => _multiFrame.Seek(value);

    internal void ToggleEyeBlinkPlayback()
    {
        if (!CanPreviewEyeBlink) return;
        _eyeBlinkActive = true;
        _eyeBlink.TogglePlayback();
    }

    internal void SetEyeBlinkTime(float value)
    {
        if (!CanPreviewEyeBlink) return;
        _eyeBlinkActive = true;
        _eyeBlink.Seek(value);
    }

    internal void ClearEyeBlinkPreview()
    {
        if (!_eyeBlinkActive) return;
        _eyeBlinkActive = false;
        ApplyEyeBlink();
        _eyeBlink.Reset();
    }

    internal void SetVisemeSelection(int index)
    {
        index = Mathf.Clamp(index, -1, LipSyncPreviewData.VisemeCount - 1);
        if (_selectedViseme == index) return;
        _selectedViseme = index;
        ApplyLipSync();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    internal void SetVisemeHover(VisemeHoverSource source, int index)
    {
        index = Mathf.Clamp(index, -1, LipSyncPreviewData.VisemeCount - 1);
        var sameSource = _visemeHoverSource == source;
        if (index < 0 && !sameSource) return;
        if (index == _hoveredViseme && sameSource) return;

        _visemeHoverSource = index < 0 ? null : source;
        _hoveredViseme = index;
        ApplyLipSync();
        RepaintOverlay();
    }

    private void OnSelectionChanged()
    {
        using var _ = new Utils.ProfilingSampleScope("Preview.SelectionChanged");
        _selection = Selection.objects.Length == 1 ? Selection.objects[0] : null;
        if (_suspendDepth == 0)
            RebuildSession(resetControls: true);
    }

    private void RebuildFromSelection()
    {
        if (_suspendDepth == 0)
            RebuildSession(resetControls: false);
    }

    private void RebuildSession(bool resetControls)
    {
        using var _ = new Utils.ProfilingSampleScope("Preview.RebuildSession");
        var previousRoot = resetControls ? null : CurrentAvatar?.Root;
        DisposeSession();
        if (resetControls) ResetControls();
        if (_selection == null || !PreviewEnabled(_selection))
        {
            DisableTimelines();
            NotifyTargetsChanged();
            return;
        }

        _session = SelectedShapesPreviewSession.Create(
            _selection,
            _expressionLayer,
            RebuildFromSelection);
        var avatars = Data?.Avatars;
        if (avatars == null || avatars.Count == 0)
        {
            DisableTimelines();
            NotifyTargetsChanged();
            return;
        }

        var previousAvatar = avatars.FirstOrDefault(avatar => avatar.Root == previousRoot);
        _currentAvatar = previousAvatar ?? avatars[0];
        ConfigureTimelines(resetControls);
        if (!CanPreviewLipSync) ResetVisemes();
        ApplyAll();
        NotifyTargetsChanged();
    }

    private void NotifyTargetsChanged()
    {
        TargetsChanged?.Invoke();
        RepaintOverlay();
    }

    private void ConfigureTimelines(bool restartMultiFrame)
    {
        var multiFrame = CurrentAvatar?.Facial?.MultiFrame;
        _multiFrame.Configure(
            multiFrame?.Duration ?? 0f,
            multiFrame?.IsLooping ?? false);
        _eyeBlink.Configure(CurrentAvatar?.EyeBlink?.Duration ?? 0f, false);
        if (restartMultiFrame && multiFrame != null)
            _multiFrame.Restart();
        if (!CanPreviewEyeBlink) _eyeBlinkActive = false;
    }

    private void DisableTimelines()
    {
        _multiFrame.Configure(0f, false);
        _eyeBlink.Configure(0f, false);
    }

    private static bool PreviewEnabled(Object selection)
    {
        var isClip = selection is AnimationClip;
        var isProjectSelection = isClip || EditorUtility.IsPersistent(selection);
        return isProjectSelection
            ? ProjectSettings.EnableProjectSelectedExpressionPreview
            : ProjectSettings.EnableHierarchySelectedExpressionPreview;
    }

    private void ResetControls()
    {
        DisableTimelines();
        _eyeBlinkActive = false;
        ResetVisemes();
    }

    private void ResetVisemes()
    {
        _selectedViseme = -1;
        _hoveredViseme = -1;
        _visemeHoverSource = null;
    }

    private void OnEyeBlinkCompleted()
        => ClearEyeBlinkPreview();

    private void ApplyAll()
    {
        ApplyFacial();
        ApplyEyeBlink();
        ApplyLipSync();
    }

    private void ApplyFacial()
    {
        var avatar = CurrentAvatar;
        var facial = avatar?.Facial;
        if (avatar == null || facial == null) return;

        var time = facial.MultiFrame == null
            ? 0f
            : facial.MultiFrame.Duration * _multiFrame.NormalizedTime;
        var shapes = BlendShapeAnimationPreview.Evaluate(facial.Animations, time);
        var apply = new BlendShapeApply(
            shapes,
            facial.DefaultWeight,
            avatar.IgnoredNames);
        _expressionLayer.Set(avatar.FaceRenderer, apply);
    }

    private void ApplyEyeBlink()
    {
        var avatar = CurrentAvatar;
        var eyeBlink = avatar?.EyeBlink;
        if (avatar == null) return;
        if (!_eyeBlinkActive || eyeBlink == null)
        {
            _eyeBlinkLayer.Clear(avatar.FaceRenderer);
            return;
        }

        if (eyeBlink.ClosedShapes != null)
        {
            var apply = new BlendShapeApply(
                eyeBlink.ClosedShapes,
                IgnoredNames: avatar.IgnoredNames);
            var opacity = eyeBlink.SimpleOpacity(_eyeBlink.NormalizedTime);
            _eyeBlinkLayer.Set(avatar.FaceRenderer, apply, opacity);
            return;
        }

        var time = eyeBlink.Duration * _eyeBlink.NormalizedTime;
        var shapes = BlendShapeAnimationPreview.Evaluate(eyeBlink.Animations, time);
        var animationApply = new BlendShapeApply(
            shapes,
            IgnoredNames: avatar.IgnoredNames);
        _eyeBlinkLayer.Set(avatar.FaceRenderer, animationApply);
    }

    private void ApplyLipSync()
    {
        var avatar = CurrentAvatar;
        var lipSync = avatar?.LipSync;
        if (avatar == null) return;

        var viseme = _hoveredViseme >= 0 ? _hoveredViseme : _selectedViseme;
        if (viseme < 0 || lipSync == null)
        {
            _lipSyncCancellerLayer.Clear(avatar.FaceRenderer);
            _lipSyncVisemeLayer.Clear(avatar.FaceRenderer);
            return;
        }

        var ignoredNames = avatar.IgnoredNames;
        var canceller = new BlendShapeApply(
            lipSync.Canceller,
            IgnoredNames: ignoredNames);
        var visemeShapes = new BlendShapeApply(
            lipSync.Visemes[viseme],
            IgnoredNames: ignoredNames);
        _lipSyncCancellerLayer.Set(avatar.FaceRenderer, canceller);
        _lipSyncVisemeLayer.Set(avatar.FaceRenderer, visemeShapes);
    }

    private void DisposeSession()
    {
        if (_session == null) return;
        ClearCurrentPreview();
        _session.Dispose();
        _session = null;
        _currentAvatar = null;
    }

    private void ClearCurrentPreview()
    {
        var renderer = CurrentAvatar?.FaceRenderer;
        if (renderer == null) return;
        _expressionLayer.Clear(renderer);
        _eyeBlinkLayer.Clear(renderer);
        _lipSyncCancellerLayer.Clear(renderer);
        _lipSyncVisemeLayer.Clear(renderer);
    }

    private static void RepaintOverlay()
        => SceneView.RepaintAll();
}
