namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(BlendShapeWeight))]
internal sealed class BlendShapeWeightDrawer : PropertyDrawer
{
    private const float PreferredNameRatio = .5f;
    private const float MinimumNameWidth = 64f;
    private const float MinimumValueWidth = 64f;
    private const float SliderWithNumberWidth = 90f;
    private const float SliderNumberWidth = 38f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
        position.SetSingleHeight();
        var nameWidth = position.width * PreferredNameRatio;
        if (position.width >= MinimumNameWidth + MinimumValueWidth)
            nameWidth = Mathf.Clamp(nameWidth, MinimumNameWidth, position.width - MinimumValueWidth);
        var nameRect = new Rect(position.x, position.y, nameWidth, position.height);
        var valueRect = new Rect(
            nameRect.xMax,
            position.y,
            Mathf.Max(0f, position.xMax - nameRect.xMax),
            position.height);

        BlendShapeNameGUI.Draw(
            nameRect,
            property.FindPropertyRelative(BlendShapeWeight.NamePropName));

        var weight = property.FindPropertyRelative(BlendShapeWeight.WeightPropName);
        var value = weight.floatValue;
        EditorGUI.BeginChangeCheck();
        if (valueRect.width >= SliderWithNumberWidth)
        {
            var (slider, number) = valueRect.SplitRight(SliderNumberWidth);
            value = GUI.HorizontalSlider(slider, value, 0f, 100f);
            value = Mathf.Clamp(EditorGUI.FloatField(number, value), 0f, 100f);
        }
        else
        {
            value = GUI.HorizontalSlider(valueRect, value, 0f, 100f);
        }
        if (EditorGUI.EndChangeCheck()) weight.floatValue = value;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.LineHeight;
}
