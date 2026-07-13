namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(DataComponent))]
internal sealed class DataComponentEditor : FaceTuneEditor<DataComponent>
{
    private bool _expressionExpanded = true;
    private bool _otherExpressionExpanded;
    private ExpressionGUIOptions Options => new("data.expression.section.label".LG());

    private void OnEnable()
    {
        _otherExpressionExpanded = targets
            .Cast<DataComponent>()
            .Any(component => ExpressionGUI.HasExternalSource(component, component.Data, component.DataReference));
    }

    protected override float GetInspectorHeight()
        => ExpressionGUI.GetHeight(
            serializedObject.FindProperty(nameof(DataComponent.Data)),
            _expressionExpanded,
            _otherExpressionExpanded,
            Options);

    protected override void DrawInspector(Rect position)
    {
        ExpressionGUI.Draw(
            position,
            serializedObject,
            Component,
            targets.Length,
            ref _expressionExpanded,
            ref _otherExpressionExpanded,
            Options);
    }
}
