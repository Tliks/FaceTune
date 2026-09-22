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
    private readonly BlendShapeWeightSet _previewSet;

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
    private int _previewListIndex;

    public void SetNormalizedTime(float value)
    {
        _normalizedTime = Mathf.Clamp01(value);
        RequestShapeRefresh();
    }

    private int _currentAppliedHoverIndex = -1;
    private bool _needsShapeRefresh = false;

    private EditingShapesPreview Preview => DirectBlendShapePreview.Instance.Editing;

    public PreviewManager(FacialShapesEditorContext context, VisualElement rootElement)
    {
        _context = context;
        _rootElement = rootElement;
        _renderer = context.Renderer;
        _previewSet = new();
        _normalizedTime = context.InitialPreviewTime;
        _previewListIndex = context.DataManagers.Count > 1 ? 1 : 0;
        SetBlendShapeTo100OnHover = true;
        HighlightBlendShapeVerticesOnHover = false;

        
        foreach (var dataManager in context.DataManagers)
            dataManager.OnAnyDataChange += RequestShapeRefresh;
        context.ActiveListChanged += OnActiveListChanged;
        OnSetBlendShapeTo100OnHoverChanged += _ => RequestShapeRefresh();
        
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
            GetCurrentSet(_previewSet);
            RefreshPreview();
            RequestShapeRefresh();
        }
    }

    private void RequestShapeRefresh()
    {
        _needsShapeRefresh = true;
    }

    private void OnActiveListChanged()
    {
        if (_context.ActiveListIndex > 0)
            _previewListIndex = _context.ActiveListIndex;
        RequestShapeRefresh();
    }

    private void CheckAndApplyUpdates()
    {
        try
        {
            if (!_isEnabled) return;

            var hoverIndexChanged = _currentHoveredIndex != _currentAppliedHoverIndex;
            var shouldRefresh = hoverIndexChanged || _needsShapeRefresh;

            if (!shouldRefresh)
            {
                return;
            }
            
            _needsShapeRefresh = false;
            _currentAppliedHoverIndex = _currentHoveredIndex;
            var index = _currentAppliedHoverIndex;

            GetCurrentSet(_previewSet);
            if (SetBlendShapeTo100OnHover && index != -1)
            {
                var key = DataManager.AllKeys[index];
                _previewSet.Add(new BlendShapeWeight(key, 100));
            }
            RefreshPreview();
        }
        catch (Exception e)
        {
            Debug.LogError($"CheckAndApplyUpdates: {e}");
        }
    }

    private void RefreshPreview()
    {
        if (_renderer == null) return;
        var ignoredNames = _context.UsesFacialIgnoredNames
            ? DataManager.ExplicitlyExcluded.ToImmutableHashSet(StringComparer.Ordinal)
            : ImmutableHashSet<string>.Empty;
        Preview.Refresh(new BlendShapeApply(
            new ImmutableBlendShapeWeightSet(_previewSet),
            0f,
            ignoredNames));
    }

    private void GetCurrentSet(BlendShapeWeightSet result)
    {
        result.Clear();
        result.AddRange(_context.Background);
        switch (_context.Mode)
        {
            case ShapesEditorMode.Facial:
                result.AddRange(DataManager.EffectiveBaseSet);
                DataManager.GetTargetValues(result);
                break;
            case ShapesEditorMode.EyeBlinkSimple:
                var closed = new BlendShapeWeightSet();
                AddTargetValues(closed, _context.DataManagers[1]);
                AddTargetValues(closed, _context.DataManagers[0]);
                foreach (var shape in closed)
                {
                    var openWeight = result.TryGetValue(shape.Name, out var open)
                        ? open.Weight
                        : 0f;
                    result.Add(new BlendShapeWeight(
                        shape.Name,
                        Mathf.Lerp(openWeight, shape.Weight, _normalizedTime)));
                }
                break;
            case ShapesEditorMode.EyeBlinkCustom:
                var animations = new List<BlendShapeWeightAnimation>();
                DataManager.GetTargetAnimations(animations);
                animations.RemoveAll(animation =>
                {
                    var index = DataManager.GetIndexForShape(animation.Name);
                    return index < 0 || DataManager.IsUnavailable(index);
                });
                result.AddRange(BlendShapeAnimationPreview.Evaluate(
                    animations,
                    BlendShapeAnimationPreview.GetDuration(animations) * _normalizedTime));
                break;
            case ShapesEditorMode.LipSync:
                AddTargetValues(result, _context.DataManagers[0]);
                if (_previewListIndex > 0)
                    AddTargetValues(result, _context.DataManagers[_previewListIndex]);
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
        _context.ActiveListChanged -= OnActiveListChanged;
        _updateScheduler?.Pause();
        Preview.Stop();
    }
}