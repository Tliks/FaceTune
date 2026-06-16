using UnityEngine.UIElements;
using Aoyon.FaceTune.Preview;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class PreviewManager : IDisposable
{
    private readonly BlendShapeOverrideManager _blendShapeOverrideManager;
    
    private readonly VisualElement _rootElement;
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

    private int _currentAppliedHoverIndex = -1;
    private bool _needsShapeRefresh = false;

    public PreviewManager(BlendShapeOverrideManager blendShapeOverrideManager, VisualElement rootElement)
    {
        _blendShapeOverrideManager = blendShapeOverrideManager;
        _rootElement = rootElement;
        _previewSet = new();
        SetBlendShapeTo100OnHover = true;
        HighlightBlendShapeVerticesOnHover = false;

        
        _blendShapeOverrideManager.OnAnyDataChange += RequestShapeRefresh;
        OnSetBlendShapeTo100OnHoverChanged += (value) => { RequestShapeRefresh(); };
        
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
            EditingShapesPreview.Start(renderer);
            GetCurrentSet(_previewSet);
            EditingShapesPreview.Refresh(_previewSet, 0);
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
                _previewSet.Add(new BlendShapeWeight(key, 100));
            }
            EditingShapesPreview.Refresh(_previewSet, 0);
        }
        catch (Exception e)
        {
            Debug.LogError($"CheckAndApplyUpdates: {e}");
        }
    }

    private void GetCurrentSet(BlendShapeWeightSet result)
    {
        result.Clear();
        result.AddRange(_blendShapeOverrideManager.BaseSet);
        _blendShapeOverrideManager.GetCurrentOverrides(result);
    }

    public void Dispose()
    {
        _isEnabled = false;
        _blendShapeOverrideManager.OnAnyDataChange -= RequestShapeRefresh;
        _updateScheduler?.Pause();
        EditingShapesPreview.Stop();
    }
}