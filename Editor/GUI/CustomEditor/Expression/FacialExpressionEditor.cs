namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FacialExpressionComponent))]
internal class FacialExpressionEditor : FaceTuneCustomEditorBase<FacialExpressionComponent>
{

    private SerializedProperty _facialSettingsProperty = null!;
    private SerializedProperty _sourceModeProperty = null!;
    private SerializedProperty _blendShapeAnimationsProperty = null!;
    private SerializedProperty _clipProperty = null!;
    private SerializedProperty _clipExcludeOptionProperty = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        _facialSettingsProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.FacialSettings));
        _sourceModeProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.SourceMode));
        _blendShapeAnimationsProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.BlendShapeAnimations));
        _clipProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.Clip));
        _clipExcludeOptionProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.ClipExcludeOption));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_facialSettingsProperty);
        EditorGUILayout.PropertyField(_sourceModeProperty);
        if (_sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.Manual)
        {
            EditorGUILayout.PropertyField(_blendShapeAnimationsProperty);
        }
        else if (_sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.FromAnimationClip)
        {
            EditorGUILayout.PropertyField(_clipProperty);
            EditorGUILayout.PropertyField(_clipExcludeOptionProperty);
        }
        serializedObject.ApplyModifiedProperties();

        if (_sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.Manual)
        {   
            if (GUILayout.Button("Open Editor"))
            {
                OpenFacialShapesEditor();
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

    private void OpenFacialShapesEditor()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var defaultBlendShapes = context.DEC.GetDefaultBlendShapeSet(Component.gameObject);
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, defaultBlendShapes.BlendShapes, new(Component.GetFirstFrameBlendShapeSet(context).BlendShapes));
        if (window == null) return;
        window.RegisterApplyCallback(RecieveEditorResult);
    }

    private void RecieveEditorResult(BlendShapeSet result)
    {
        Undo.RecordObject(Component, "RecieveEditorResult");
        serializedObject.Update();
        FacialExpressionEditorUtility.UpdateShapes(Component, result.BlendShapes.ToList().AsReadOnly());
        serializedObject.ApplyModifiedProperties();
    }

    [MenuItem($"CONTEXT/{nameof(FacialExpressionComponent)}/ToClip")]
    private static void ToClip(MenuCommand command)
    {
        var component = (command.context as FacialExpressionComponent)!;
        CustomEditorUtility.ToClip(component.gameObject, context => component.GetFirstFrameBlendShapeSet(context).BlendShapes);
    }

    private void ConvertToManual()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        Undo.RecordObject(Component, "ConvertToManual");
        var shapes = Component.GetFirstFrameBlendShapeSet(context);
        FacialExpressionEditorUtility.UpdateShapes(Component, shapes.BlendShapes.ToList());
        Component.SourceMode = AnimationSourceMode.Manual;
    }
}

public class FacialExpressionEditorUtility
{
    public static void UpdateShapes(FacialExpressionComponent component, IReadOnlyList<BlendShape> newShapes)
    {
        Undo.RecordObject(component, "UpdateShapes");
        var animations = newShapes.Select(shape => BlendShapeAnimation.SingleFrame(shape.Name, shape.Weight)).ToList();
        component.BlendShapeAnimations = animations;
    }
}
