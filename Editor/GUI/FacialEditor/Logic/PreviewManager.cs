using UnityEngine.UIElements;
using aoyon.facetune.preview;

namespace aoyon.facetune.gui.shapes_editor;

internal class PreviewManager : IDisposable
{
    private readonly SkinnedMeshRenderer _renderer;
    private readonly BlendShapeOverrideManager _blendShapeOverrideManager;
    private readonly FacialShapeUI _ui;
    private readonly BlendShapeSet _previewSet;
    private readonly HighlightBlendShapeProcessor _highlightBlendShapeProcessor;
    
    private readonly VisualElement _rootElement;
    private IVisualElementScheduledItem _updateScheduler;
    
    private int _currentAppliedHoverIndex = -2;
    private int _pendingHoverIndex = -2;
    private bool _needsShapeRefresh = false;
    
    private const int UpdateIntervalMs = 33; // 約30fps
    
    private bool _setBlendShapeTo100OnHover => _ui.GeneralControls.SetBlendShapeTo100OnHover;
    private bool _highlightBlendShapeVerticesOnHover => _ui.GeneralControls.HighlightBlendShapeVerticesOnHover;

    public PreviewManager(SkinnedMeshRenderer renderer, BlendShapeOverrideManager blendShapeOverrideManager, FacialShapeUI ui)
    {
        _renderer = renderer;
        _blendShapeOverrideManager = blendShapeOverrideManager;
        _ui = ui;
        _rootElement = ui.Root;
        _previewSet = new();
        _highlightBlendShapeProcessor = new HighlightBlendShapeProcessor(_renderer, _renderer.sharedMesh);
        
        _blendShapeOverrideManager.OnAnyDataChange += RequestShapeRefresh;
        _ui.UnselectedPanel.OnHoveredIndexChanged += OnHoveredIndexChanged;
        _ui.GeneralControls.OnSetBlendShapeTo100OnHoverChanged += (value) => { RequestShapeRefresh(); };
        _ui.GeneralControls.OnHighlightBlendShapeVerticesOnHoverChanged += (value) => { _highlightBlendShapeProcessor.ClearHighlight(); };
        
        // UI Elementsスケジューラーで定期的に両方の更新をチェック
        // UpdateIntervalMsで更新の頻度を制限する
        _updateScheduler = _rootElement.schedule
            .Execute(CheckAndApplyUpdates)
            .Every(UpdateIntervalMs);
            
        var defaultSet = new BlendShapeSet();
        GetCurrentSet(defaultSet);
        EditingShapesPreview.Start(_renderer, defaultSet);
    }

    private void RequestShapeRefresh()
    {
        _needsShapeRefresh = true;
    }

    private void OnHoveredIndexChanged(int index)
    {
        _pendingHoverIndex = index;
    }

    private void CheckAndApplyUpdates()
    {
        var hoverIndexChanged = _pendingHoverIndex != _currentAppliedHoverIndex;
        var shouldRefresh = hoverIndexChanged || _needsShapeRefresh;

        if (!shouldRefresh)
        {
            return;
        }
        
        _needsShapeRefresh = false;
        _currentAppliedHoverIndex = _pendingHoverIndex;
        var index = _currentAppliedHoverIndex;

        GetCurrentSet(_previewSet);
        if (_setBlendShapeTo100OnHover && index != -1)
        {
            _previewSet.Add(new BlendShape(_blendShapeOverrideManager.AllKeys[index], 100));
        }
        EditingShapesPreview.Refresh(_previewSet);

        if (_highlightBlendShapeVerticesOnHover)
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
        _blendShapeOverrideManager.OnAnyDataChange -= RequestShapeRefresh;
        _ui.UnselectedPanel.OnHoveredIndexChanged -= OnHoveredIndexChanged;
        _updateScheduler?.Pause();
        EditingShapesPreview.Stop();
        _highlightBlendShapeProcessor.Dispose();
    }
}