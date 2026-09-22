using UnityEngine.UIElements;
using Aoyon.FaceTune.Preview;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class PreviewManager : IDisposable
{
    private readonly FacialShapesEditorContext _context;
    private BlendShapeOverrideManager DataManager => _context.DataManager;

    private readonly VisualElement _rootElement;
    private readonly SkinnedMeshRenderer? _renderer;
    private IVisualElementScheduledItem _updateScheduler;
    private const int UpdateIntervalMs = 33; // 約30fps
    private readonly BlendShapeWeightSet _backgroundSet;
    private readonly BlendShapeWeightSet _previewSet;
    private readonly BlendShapeWeightSet _hoverSet;
    private float _previewOpacity = 1f;

    private bool _setBlendShapeTo100OnHover;
    public bool SetBlendShapeTo100OnHover
    {
        get => _setBlendShapeTo100OnHover;
        set
        {
            if (SetBlendShapeTo100OnHover == value) return;
            _setBlendShapeTo100OnHover = value;
            OnSetBlendShapeTo100OnHoverChanged?.Invoke(value);
        }
    }
    private bool _highlightBlendShapeVerticesOnHover;
    public bool HighlightBlendShapeVerticesOnHover
    {
        get => _highlightBlendShapeVerticesOnHover;
        set
        {
            if (HighlightBlendShapeVerticesOnHover == value) return;
            _highlightBlendShapeVerticesOnHover = value;
            OnHighlightBlendShapeVerticesOnHoverChanged?.Invoke(value);
        }
    }

    private int _currentHoveredIndex = -1;
    public int CurrentHoveredIndex
    {
        get => _currentHoveredIndex;
        set
        {
            if (_currentHoveredIndex == value) return;
            _currentHoveredIndex = value;
            OnHoveredIndexChanged?.Invoke(_currentHoveredIndex);
        }
    }

    public event Action<bool>? OnSetBlendShapeTo100OnHoverChanged;
    public event Action<bool>? OnHighlightBlendShapeVerticesOnHoverChanged;
    public event Action<int>? OnHoveredIndexChanged;

    private bool _isEnabled = false;
    private float _normalizedTime;

    public void SetNormalizedTime(float value)
    {
        _normalizedTime = Mathf.Clamp01(value);
        RequestShapeRefresh();
    }

    private int _currentAppliedHoverIndex = -1;
    private bool _previewDirty;

    private EditingShapesPreview Preview => DirectBlendShapePreview.Instance.Editing;

    public PreviewManager(FacialShapesEditorContext context, VisualElement rootElement)
    {
        _context = context;
        _rootElement = rootElement;
        _renderer = context.Renderer;
        _backgroundSet = new();
        _previewSet = new();
        _hoverSet = new();
        _normalizedTime = context.InitialPreviewTime;
        SetBlendShapeTo100OnHover = true;
        HighlightBlendShapeVerticesOnHover = false;

        
        foreach (var dataManager in context.DataManagers)
            dataManager.OnAnyDataChange += RequestShapeRefresh;
        context.ActiveListChanged += RequestShapeRefresh;
        context.LipSyncChanged += RequestShapeRefresh;
        OnSetBlendShapeTo100OnHoverChanged += _ =>
            _currentAppliedHoverIndex = int.MinValue;
        
        // UI Elementsスケジューラーで定期的に両方の更新をチェック
        // UpdateIntervalMsで更新の頻度を制限する
        _updateScheduler = _rootElement.schedule
            .Execute(CheckAndApplyUpdates)
            .Every(UpdateIntervalMs);

        InitializeTargetRenderer(_renderer);
    }

    private void InitializeTargetRenderer(SkinnedMeshRenderer? renderer)
    {
        Preview.Stop();
        if (renderer == null)
        {
            _isEnabled = false;
        }
        else
        {
            _isEnabled = true;
            Preview.Start(renderer);
            SetBackground();
            BuildPreviewSet();
            SetPreview();
            SetHover();
        }
    }

    private void RequestShapeRefresh()
    {
        _previewDirty = true;
    }

    private void CheckAndApplyUpdates()
    {
        try
        {
            if (!_isEnabled) return;

            if (_previewDirty)
            {
                _previewDirty = false;
                BuildPreviewSet();
                SetPreview();
            }

            if (_currentHoveredIndex != _currentAppliedHoverIndex)
            {
                _currentAppliedHoverIndex = _currentHoveredIndex;
                SetHover();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CheckAndApplyUpdates: {e}");
        }
    }

    private ImmutableHashSet<string> IgnoredNames
        => _context.UsesFacialIgnoredNames
            ? DataManager.ExplicitlyExcluded.ToImmutableHashSet(StringComparer.Ordinal)
            : _context.IgnoredNames;

    private void SetBackground()
    {
        if (_renderer == null) return;
        _backgroundSet.Clear();
        _backgroundSet.AddRange(_context.Background);
        Preview.SetBackground(_context.UsesFacialIgnoredNames
            ? BlendShapeApply.Empty
            : new BlendShapeApply(
                new ImmutableBlendShapeWeightSet(_backgroundSet),
                _context.BackgroundDefaultValue,
                IgnoredNames));
    }

    private void SetPreview()
    {
        if (_renderer == null) return;
        Preview.SetPreview(
            new BlendShapeApply(
                new ImmutableBlendShapeWeightSet(_previewSet),
                _context.UsesFacialIgnoredNames ? 0f : null,
                IgnoredNames),
            _previewOpacity);
    }

    private void SetHover()
    {
        if (_renderer == null) return;
        _hoverSet.Clear();
        if (SetBlendShapeTo100OnHover && _currentAppliedHoverIndex != -1)
        {
            var key = DataManager.AllKeys[_currentAppliedHoverIndex];
            _hoverSet.Add(new BlendShapeWeight(key, 100));
        }
        Preview.SetHover(new BlendShapeApply(
            new ImmutableBlendShapeWeightSet(_hoverSet),
            IgnoredNames: IgnoredNames));
    }

    private void BuildPreviewSet()
    {
        _previewSet.Clear();
        _previewOpacity = 1f;
        switch (_context.Mode)
        {
            case ShapesEditorMode.Facial:
                _previewSet.AddRange(DataManager.EffectiveBaseSet);
                DataManager.GetTargetValues(_previewSet);
                break;
            case ShapesEditorMode.EyeBlinkSimple:
                AddTargetValues(_previewSet, _context.DataManagers[1]);
                AddTargetValues(_previewSet, _context.DataManagers[0]);
                _previewOpacity = _normalizedTime;
                break;
            case ShapesEditorMode.EyeBlinkCustom:
                var animations = new List<BlendShapeWeightAnimation>();
                DataManager.GetTargetAnimations(animations);
                animations.RemoveAll(animation =>
                {
                    var index = DataManager.GetIndexForShape(animation.Name);
                    return index < 0 || DataManager.IsUnavailable(index);
                });
                _previewSet.AddRange(BlendShapeAnimationPreview.Evaluate(
                    animations,
                    BlendShapeAnimationPreview.GetDuration(animations) * _normalizedTime));
                break;
            case ShapesEditorMode.LipSync:
                AddTargetValues(_previewSet, _context.DataManagers[0]);
                if (_context.PreviewLipSync is { } lipSync)
                {
                    foreach (var shape in lipSync.GetOrderedShapes()[_context.PreviewViseme])
                    {
                        if (!_context.LipSyncUnavailableNames.Contains(shape.Name))
                            _previewSet.Add(shape);
                    }
                }
                break;
        }
    }

    private static void AddTargetValues(
        BlendShapeWeightSet result,
        BlendShapeOverrideManager dataManager)
    {
        foreach (var index in dataManager.GetTargetIndices(i => !dataManager.IsUnavailable(i)))
        {
            result.Add(new BlendShapeWeight(
                dataManager.AllKeys[index],
                dataManager.GetShapeWeight(index)));
        }
    }

    public void Dispose()
    {
        _isEnabled = false;
        foreach (var dataManager in _context.DataManagers)
            dataManager.OnAnyDataChange -= RequestShapeRefresh;
        _context.ActiveListChanged -= RequestShapeRefresh;
        _context.LipSyncChanged -= RequestShapeRefresh;
        _updateScheduler?.Pause();
        Preview.Stop();
    }
}