namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AnimationExpressionComponent))]
internal class AnimationExpressionEditor : FaceTuneCustomEditorBase<AnimationExpressionComponent>
{

    private SerializedProperty _sourceModeProperty = null!;
    private SerializedProperty _genericAnimationsProperty = null!;
    private SerializedProperty _clipProperty = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        _sourceModeProperty = serializedObject.FindProperty(nameof(AnimationExpressionComponent.SourceMode));
        _genericAnimationsProperty = serializedObject.FindProperty(nameof(AnimationExpressionComponent.GenericAnimations));
        _clipProperty = serializedObject.FindProperty(nameof(AnimationExpressionComponent.Clip));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_sourceModeProperty);
        if (_sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.Manual)
        {
            EditorGUILayout.PropertyField(_genericAnimationsProperty);
        }
        else if (_sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.FromAnimationClip)
        {
            EditorGUILayout.PropertyField(_clipProperty);
        }
        serializedObject.ApplyModifiedProperties();

        if (_sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.Manual)
        {   
            if (GUILayout.Button("Open Editor"))
            {
                OpenGenericAnimationsEditor();
            }
        }
        else if (_sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.FromAnimationClip)
        {
            if (GUILayout.Button("Convert to Manual"))
            {
                ConvertToManual();
            }
        }
    }

    private void OpenGenericAnimationsEditor()
    {
        // TODO:
    }

    [MenuItem($"CONTEXT/{nameof(AnimationExpressionComponent)}/ToClip")]
    private static void ToClip(MenuCommand command)
    {
        var component = (command.context as AnimationExpressionComponent)!;
        CustomEditorUtility.ToClip(component.GenericAnimations);
    }

    private void ConvertToManual()
    {
        var clip = Component.Clip;
        if (clip == null) return;

        var genericAnimations = GenericAnimation.FromAnimationClip(clip);
        AnimationExpressionEditorUtility.UpdateAnimations(Component, genericAnimations);
        Component.SourceMode = AnimationSourceMode.Manual;
    }
}

public class AnimationExpressionEditorUtility
{
    public static void UpdateAnimations(AnimationExpressionComponent component, IReadOnlyList<GenericAnimation> newAnimations)
    {
        Undo.RecordObject(component, "UpdateAnimations");
        component.GenericAnimations = newAnimations.ToList();
    }
}
