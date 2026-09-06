namespace Aoyon.FaceTune.Preview;

internal class EditingShapesPreview
{
    private readonly DirectBlendShapePreviewContext _preview;
    private SkinnedMeshRenderer? _target;

    internal EditingShapesPreview(DirectBlendShapePreviewContext preview)
    {
        _preview = preview;
    }

    public void Start(SkinnedMeshRenderer? target)
    {
        if (_target != null && _target != target)
            _preview.Clear(_target);

        _target = target;
    }

    public void Refresh(BlendShapeApply apply)
    {
        if (_target != null)
            _preview.Set(_target, apply);
    }

    public void Stop()
    {
        if (_target != null)
            _preview.Clear(_target);
        _target = null;
    }
}
