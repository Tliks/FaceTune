using aoyon.facetune.preview;

namespace aoyon.facetune.ui.shapes_editor;

internal class PreviewManager : IDisposable
{
    private readonly SkinnedMeshRenderer _renderer;
    private readonly BlendShapeOverrideManager _blendShapeOverrideManager;
    private readonly FacialShapeUI _ui;
    private readonly BlendShapeSet _previewSet;
    private readonly HighlightBlendShapeProcessor _highlightBlendShapeProcessor;

    public PreviewManager(SkinnedMeshRenderer renderer, BlendShapeOverrideManager blendShapeOverrideManager, FacialShapeUI ui)
    {
        _renderer = renderer;
        _blendShapeOverrideManager = blendShapeOverrideManager;
        _ui = ui;
        _previewSet = new();
        _highlightBlendShapeProcessor = new HighlightBlendShapeProcessor(_renderer, _renderer.sharedMesh);
        _blendShapeOverrideManager.OnAnyDataChange += RefreshShapePreview;
        _ui.UnselectedPanel.OnHoveredIndexChanged += RefreshPreviewOnHover;
        _ui.GeneralControls.OnSetBlendShapeTo100OnHoverChanged += (value) => { RefreshShapePreview(); };
        _ui.GeneralControls.OnHighlightBlendShapeVerticesOnHoverChanged += (value) => { _highlightBlendShapeProcessor.ClearHighlight(); };
        var defaultSet = new BlendShapeSet();
        GetCurrentSet(defaultSet);
        EditingShapesPreview.Start(_renderer, defaultSet);
    }

    private void RefreshShapePreview()
    {
        GetCurrentSet(_previewSet);
        EditingShapesPreview.Refresh(_previewSet);
    }

    private void RefreshPreviewOnHover(int index)
    {
        if (_ui.GeneralControls.SetBlendShapeTo100OnHover)
        {
            GetCurrentSet(_previewSet);
            if (index != -1)
            {
                _previewSet.Add(new BlendShape(_blendShapeOverrideManager.AllKeys[index], 100));
            }
            EditingShapesPreview.Refresh(_previewSet);
        }
        if (_ui.GeneralControls.HighlightBlendShapeVerticesOnHover)
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
        _blendShapeOverrideManager.OnAnyDataChange -= RefreshShapePreview;
        _ui.UnselectedPanel.OnHoveredIndexChanged -= RefreshPreviewOnHover;
        EditingShapesPreview.Stop();
        _highlightBlendShapeProcessor.Dispose();
    }
}