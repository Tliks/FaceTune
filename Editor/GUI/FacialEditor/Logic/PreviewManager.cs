using UnityEngine.UIElements;
using Aoyon.FaceTune.Preview;

namespace Aoyon.FaceTune.Gui.shapes_editor;

internal class PreviewManager : IDisposable
{
    private readonly BlendShapeOverrideManager _blendShapeOverrideManager;
    private  HighlightBlendShapeProcessor _highlightBlendShapeProcessor;
    
    private readonly VisualElement _rootElement;
    private IVisualElementScheduledItem _updateScheduler;
    private const int UpdateIntervalMs = 33; // 約30fps
    private readonly BlendShapeSet _previewSet;

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

    private int _currentAppliedHoverIndex = -1;
    private bool _needsShapeRefresh = false;

    public PreviewManager(BlendShapeOverrideManager blendShapeOverrideManager, VisualElement rootElement)
    {
        _blendShapeOverrideManager = blendShapeOverrideManager;
        _rootElement = rootElement;
        _previewSet = new();
        _highlightBlendShapeProcessor = new HighlightBlendShapeProcessor();
        SetBlendShapeTo100OnHover = true;
        HighlightBlendShapeVerticesOnHover = false;

        
        _blendShapeOverrideManager.OnAnyDataChange += RequestShapeRefresh;
        OnSetBlendShapeTo100OnHoverChanged += (value) => { RequestShapeRefresh(); };
        OnHighlightBlendShapeVerticesOnHoverChanged += (value) => { _highlightBlendShapeProcessor.ClearHighlight(); };
        
        // UI Elementsスケジューラーで定期的に両方の更新をチェック
        // UpdateIntervalMsで更新の頻度を制限する
        _updateScheduler = _rootElement.schedule
            .Execute(CheckAndApplyUpdates)
            .Every(UpdateIntervalMs);
        
    }

    public void RefreshTargetRenderer(SkinnedMeshRenderer? renderer)
    {
        EditingShapesPreview.Stop();
        if (renderer == null)
        {
            _isEnabled = false;
        }
        else
        {
            _isEnabled = true;
            var defaultSet = new BlendShapeSet();
            GetCurrentSet(defaultSet);
            EditingShapesPreview.Start(renderer, defaultSet);
            _highlightBlendShapeProcessor.RefreshTarget(renderer, renderer.sharedMesh);
            RequestShapeRefresh();
        }
    }

    private void RequestShapeRefresh()
    {
        _needsShapeRefresh = true;
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
                var key = _blendShapeOverrideManager.AllKeys[index];
                _previewSet.Add(new BlendShape(key, 100));
            }
            EditingShapesPreview.Refresh(_previewSet);

            if (HighlightBlendShapeVerticesOnHover)
            {
                if (index != -1)
                {
                    _highlightBlendShapeProcessor.HilightBlendShapeFor(index);
                }
                else
                {
                    _highlightBlendShapeProcessor.ClearHighlight();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CheckAndApplyUpdates: {e}");
        }
    }

    private void GetCurrentSet(BlendShapeSet result)
    {
        result.Clear();
        foreach (var shape in _blendShapeOverrideManager.AllKeys)
        {
            result.Add(new BlendShape(shape, 0));
        }
        foreach (var shape in _blendShapeOverrideManager.StyleSet)
        {
            result.Add(shape);
        }
        _blendShapeOverrideManager.GetCurrentOverrides(result);
    }
    public void Dispose()
    {
        _isEnabled = false;
        _blendShapeOverrideManager.OnAnyDataChange -= RequestShapeRefresh;
        _updateScheduler?.Pause();
        EditingShapesPreview.Stop();
        _highlightBlendShapeProcessor.Dispose();
    }
}