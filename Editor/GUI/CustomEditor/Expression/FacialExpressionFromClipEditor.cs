namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FacialExpressionFromClipComponent))]
internal class FacialExpressionFromClipEditor : FaceTuneCustomEditorBase<FacialExpressionFromClipComponent>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Add FacialExpression"))
        {
            Undo.AddComponent<FacialExpressionComponent>(Component.gameObject);
        }
        if (GUILayout.Button("Convert To FacialExpression"))
        {
            ConvertToFacialExpression();
        }
    }

    private void ConvertToFacialExpression()
    {
        var clip = Component.Clip;
        if (clip == null) return;
        var excludeOption = Component.ClipExcludeOption;
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var defaultExpression = context.DEC.GetDefaultExpression(Component.gameObject);
        var newBlendShapes = FacialExpressionFromClipComponent.GetBlendShapeSetFromClip(clip, excludeOption, defaultExpression.BlendShapeSet);
        var fec = Undo.AddComponent<FacialExpressionComponent>(Component.gameObject);
        FacialExpressionEditorUtility.UpdateShapes(fec, newBlendShapes.BlendShapes.ToList());
        fec.EnableBlending = Component.EnableBlending;
        fec.AllowEyeBlink = Component.AllowEyeBlink;
        fec.AllowLipSync = Component.AllowLipSync;
        Undo.RegisterCreatedObjectUndo(fec, "Convert To FacialExpression");
        Undo.DestroyObjectImmediate(Component);
    }
}