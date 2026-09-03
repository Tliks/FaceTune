namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(BlendShapeWeight))]
internal sealed class BlendShapeWeightDrawer : PropertyDrawer
{
    private const float NameRatio = .7f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
        position.SetSingleHeight();
        var (nameRect, weightRect) = position.SplitRatio(NameRatio);
        BlendShapeNameGUI.Draw(nameRect, property.FindPropertyRelative(BlendShapeWeight.NamePropName));
        EditorGUI.PropertyField(weightRect, property.FindPropertyRelative(BlendShapeWeight.WeightPropName), GUIContent.none);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.LineHeight;
}
