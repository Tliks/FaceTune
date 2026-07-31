namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(DataComponent))]
internal sealed class DataComponentEditor : FaceTuneSectionEditor<DataComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateExpressionSection() };

    private FaceTuneSection CreateExpressionSection()
    {
        var data = serializedObject.FindProperty(nameof(DataComponent.Data));
        ExpressionGUI.InitializeExpansions(data);
        return new(
            "data.expression.section.label".LG(),
            () => ExpressionGUI.GetContentHeight(data),
            content => ExpressionGUI.DrawContent(
                content,
                serializedObject,
                Component,
                targets.Length),
            true);
    }
}
