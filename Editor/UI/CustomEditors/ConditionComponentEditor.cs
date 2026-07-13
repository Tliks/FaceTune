namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ConditionComponent))]
internal sealed class ConditionComponentEditor : FaceTuneSectionEditor<ConditionComponent>
{
    protected override GUIContent SectionLabel => "condition.section.label".LG();

    protected override float GetSectionContentHeight()
        => GetPropertyHeight(nameof(ConditionComponent.Condition), true)
         - GUIHelper.VerticalSpacing;

    protected override void DrawSectionContent(Rect position)
        => DrawProperty(ref position, nameof(ConditionComponent.Condition), true);
}
