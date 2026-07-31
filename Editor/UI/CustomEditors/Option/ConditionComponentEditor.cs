namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ConditionComponent))]
internal sealed class ConditionComponentEditor : FaceTuneSectionEditor<ConditionComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateConditionSection() };

    private FaceTuneSection CreateConditionSection()
        => new(
            "condition.section.label".LG(),
            () => GetPropertyHeight(nameof(ConditionComponent.Condition), true),
            position =>
            {
                var condition = serializedObject.FindProperty(nameof(ConditionComponent.Condition));
                position.height = EditorGUI.GetPropertyHeight(condition, GUIContent.none, true);
                EditorGUI.PropertyField(position, condition, GUIContent.none, true);
            },
            true);
}
