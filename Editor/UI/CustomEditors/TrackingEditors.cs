namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(EyeBlinkComponent))]
internal sealed class AdvancedEyeBlinkEditor : FaceTuneEditor<EyeBlinkComponent>
{
}

[CanEditMultipleObjects]
[CustomEditor(typeof(LipSyncComponent))]
internal sealed class AdvancedLipSyncEditor : FaceTuneEditor<LipSyncComponent>
{
}

[CanEditMultipleObjects]
[CustomEditor(typeof(ConditionComponent))]
internal sealed class ConditionEditor : FaceTuneSectionEditor<ConditionComponent>
{
    protected override GUIContent SectionLabel => "condition.section.label".LG();

    protected override float GetSectionContentHeight()
        => GetPropertyHeight(nameof(ConditionComponent.Condition), true)
         - GUIHelper.VerticalSpacing;

    protected override void DrawSectionContent(Rect position)
        => DrawProperty(ref position, nameof(ConditionComponent.Condition), true);
}
