namespace Aoyon.FaceTune.Preview;

internal class EditingShapesPreview
{
    private readonly DirectBlendShapePreviewLayer _background;
    private readonly DirectBlendShapePreviewLayer _preview;
    private readonly DirectBlendShapePreviewLayer _hover;
    private readonly SelectedShapesPreview _selected;
    private SkinnedMeshRenderer? _target;

    internal EditingShapesPreview(
        DirectBlendShapePreviewLayer background,
        DirectBlendShapePreviewLayer preview,
        DirectBlendShapePreviewLayer hover,
        SelectedShapesPreview selected)
    {
        _background = background;
        _preview = preview;
        _hover = hover;
        _selected = selected;
    }

    public void Start(SkinnedMeshRenderer target)
    {
        if (_target != null && _target != target)
            Clear(_target);

        if (_target == null)
            _selected.Suspend();

        _target = target;
    }

    public void Refresh(
        BlendShapeApply background,
        BlendShapeApply preview,
        float previewOpacity,
        BlendShapeApply hover)
    {
        if (_target == null) return;
        _background.Set(_target, background);
        _preview.Set(_target, preview, previewOpacity);
        _hover.Set(_target, hover);
    }

    public void Stop()
    {
        if (_target == null) return;

        Clear(_target);
        _target = null;
        _selected.Resume();
    }

    private void Clear(SkinnedMeshRenderer renderer)
    {
        _background.Clear(renderer);
        _preview.Clear(renderer);
        _hover.Clear(renderer);
    }
}
