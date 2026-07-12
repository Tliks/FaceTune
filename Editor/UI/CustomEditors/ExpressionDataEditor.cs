namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(DataComponent))]
internal sealed class ExpressionDataEditor : FaceTuneEditor<DataComponent>
{
    protected override void DrawInspector()
    {
        DrawProperty(nameof(DataComponent.DataReference));
        DrawProperty(nameof(DataComponent.Data));
    }
}
