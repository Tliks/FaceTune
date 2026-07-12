namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(ExpressionSettings))]
internal sealed class ExpressionSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var mode = property.FindPropertyRelative(ExpressionSettings.MultiFrameModePropName);
        FaceTuneDrawerUtility.Enum(ref position, mode, "expression.multiFrame.mode.label", nameof(MultiFrameMode));
        if (mode.enumValueIndex == (int)MultiFrameMode.Trigger)
            FaceTuneDrawerUtility.Enum(ref position, property.FindPropertyRelative(ExpressionSettings.TriggerHandPropName), "expression.multiFrame.linkedHand.label", nameof(Hand));
        else if (mode.enumValueIndex == (int)MultiFrameMode.Parameter)
            FaceTuneDrawerUtility.Draw(ref position, property.FindPropertyRelative(ExpressionSettings.ParameterNamePropName), "expression.multiFrame.parameterName.label");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative(ExpressionSettings.MultiFrameModePropName);
        var rows = mode.enumValueIndex == (int)MultiFrameMode.Trigger
            || mode.enumValueIndex == (int)MultiFrameMode.Parameter ? 2 : 1;
        return FaceTuneDrawerUtility.Line * rows + FaceTuneDrawerUtility.Space * (rows - 1);
    }
}

[CustomPropertyDrawer(typeof(FacialSettings))]
internal sealed class FacialSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        FaceTuneDrawerUtility.Enum(ref position, property.FindPropertyRelative(FacialSettings.AllowEyeBlinkPropName), "facialSettings.allowEyeBlink.label", nameof(TrackingPermission));
        FaceTuneDrawerUtility.Enum(ref position, property.FindPropertyRelative(FacialSettings.AllowLipSyncPropName), "facialSettings.allowLipSync.label", nameof(TrackingPermission));
        FaceTuneDrawerUtility.Enum(ref position, property.FindPropertyRelative(FacialSettings.WriteModePropName), "facialSettings.writeMode.label", nameof(ExpressionWriteMode));
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => (FaceTuneDrawerUtility.Line + FaceTuneDrawerUtility.Space) * 3f;
}

[CustomPropertyDrawer(typeof(BlendShapeWeightAnimation))]
internal sealed class BlendShapeWeightAnimationDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        var (nameRect, curveRect) = position.SplitRatio(.4f);
        EditorGUI.PropertyField(nameRect, property.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName), GUIContent.none);
        EditorGUI.PropertyField(curveRect, property.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName), GUIContent.none);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => FaceTuneDrawerUtility.Line;
}

[CustomPropertyDrawer(typeof(ExpressionData))]
internal sealed class ExpressionDataDrawer : PropertyDrawer
{
    private static readonly ReorderableListOptions AnimationListOptions = new(Foldout: false, MaxVisibleHeight: 180f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        var (clipRect, optionRect) = position.SplitRatio(.62f);
        EditorGUI.PropertyField(clipRect, property.FindPropertyRelative("Clip"), GUIContent.none);
        EditorGUI.PropertyField(optionRect, property.FindPropertyRelative("ClipOption"), GUIContent.none);
        position.NewLine();
        var animations = property.FindPropertyRelative("BlendShapeAnimations");
        position.height = ReorderableListUI.GetHeight(animations, AnimationListOptions);
        ReorderableListUI.Draw(position, animations, "expression.blendShapeAnimations.label".LG(), AnimationListOptions);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => FaceTuneDrawerUtility.Line + FaceTuneDrawerUtility.Space
         + ReorderableListUI.GetHeight(property.FindPropertyRelative("BlendShapeAnimations"), AnimationListOptions);
}
