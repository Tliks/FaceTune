namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(TransformAnimation))]
internal sealed class TransformAnimationDrawer : PropertyDrawer
{
    private const float PreferredTargetRatio = .4f;
    private const float MinimumFieldWidth = 64f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.LineHeight;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        position.SetSingleHeight();
        var targetWidth = position.width * PreferredTargetRatio;
        if (position.width >= MinimumFieldWidth * 2f)
        {
            targetWidth = Mathf.Clamp(
                targetWidth,
                MinimumFieldWidth,
                position.width - MinimumFieldWidth);
        }

        var targetRect = new Rect(position.x, position.y, targetWidth, position.height);
        var curveRect = new Rect(
            targetRect.xMax,
            position.y,
            Mathf.Max(0f, position.xMax - targetRect.xMax),
            position.height);
        EditorGUI.PropertyField(
            targetRect,
            property.FindPropertyRelative(nameof(TransformAnimation.Target)),
            GUIContent.none);
        EditorGUI.PropertyField(
            curveRect,
            property.FindPropertyRelative(nameof(TransformAnimation.Curve)),
            GUIContent.none);
    }
}
