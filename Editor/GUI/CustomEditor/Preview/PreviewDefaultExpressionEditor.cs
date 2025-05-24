using com.aoyon.facetune.preview;

namespace com.aoyon.facetune.ui;

[CustomEditor(typeof(PreviewDefaultExpressionComponent))]
internal class PreviewDefaultExpressionEditor : FaceTuneCustomEditorBase<PreviewDefaultExpressionComponent>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        TogglablePreviewDrawer.Draw(DefaultShapesPreview.ToggleNode);
    }
}