namespace Aoyon.FaceTune.Preview;

internal class EditingShapesPreview
{
    private readonly DirectBlendShapePreviewLayer _preview;
    private readonly SelectedShapesPreview _selected;
    private SkinnedMeshRenderer? _target;

    internal EditingShapesPreview(
        DirectBlendShapePreviewLayer preview,
        SelectedShapesPreview selected)
    {
        _preview = preview;
        _selected = selected;
    }

    public void Start(SkinnedMeshRenderer target)
    {
        if (_target != null && _target != target)
            _preview.Clear(_target);

        if (_target == null)
            _selected.Suspend();

        _target = target;
    }

    public void Refresh(BlendShapeApply apply)
    {
        if (_target != null)
            _preview.Set(_target, apply);
    }

    public void Stop()
    {
        if (_target == null) return;

        _preview.Clear(_target);
        _target = null;
        _selected.Resume();
    }
}
