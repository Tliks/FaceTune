namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(BlendShapeWeight))]
internal sealed class BlendShapeWeightDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        var (nameRect, weightRect) = position.SplitRatio(.4f);
        EditorGUI.PropertyField(nameRect, property.FindPropertyRelative(BlendShapeWeight.NamePropName), GUIContent.none);
        EditorGUI.PropertyField(weightRect, property.FindPropertyRelative(BlendShapeWeight.WeightPropName), GUIContent.none);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.LineHeight;
}
