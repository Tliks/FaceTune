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
    }
}