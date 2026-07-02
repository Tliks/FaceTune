using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(StyleComponent))]
internal class FacialStyleEditor : FaceTuneIMGUIEditorBase<StyleComponent>
{
    private SerializedProperty _blendShapeAnimationsProperty = null!;
    private SerializedProperty _applyToRendererProperty = null!;

    private bool _hasDefault = false;
    public override void OnEnable()
    {
        base.OnEnable();
        _blendShapeAnimationsProperty = serializedObject.FindProperty(nameof(StyleComponent.BlendShapeAnimations));
        _applyToRendererProperty = serializedObject.FindProperty(nameof(StyleComponent.ApplyToRenderer));
        _hasDefault = HasDefault();
    }

    protected override void OnInnerInspectorGUI()
    {
        LocalizedPropertyField(_blendShapeAnimationsProperty);
        if (GUILayout.Button("FacialStyleComponent:button:OpenEditor".LG()))
        {
            OpenEditor();
        }
        LocalizedPropertyField(_applyToRendererProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        using (new EditorGUILayout.HorizontalScope())
        {
            var buttonWidth = (EditorGUIUtility.currentViewWidth) / 2f;
            if (GUILayout.Button("FacialStyleComponent:button:UpdateFromScene".LG(), GUILayout.Width(buttonWidth)))
            {
                UpdateFromScene();
            }
            GUI.enabled = !_hasDefault;
            if (GUILayout.Button("FacialStyleComponent:button:AsDefault".LG(), GUILayout.Width(buttonWidth)))
            {
                AsDefault();
            }
            GUI.enabled = true;
        }
    }

    private void OpenEditor()
    {
        var defaultOverride = new BlendShapeWeightSet();
        ExpressionDataUtility.AddFirstFrameBlendShapes(Component.Data, defaultOverride, string.Empty);
        CustomEditorUtility.OpenEditor(Component.gameObject, new FacialStyleTargeting(){ Target = Component }, defaultOverride, null);
    }

    private void UpdateFromScene()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var blendShapes = context.FaceRenderer.GetBlendShapeWeights(context.FaceMesh).Where(shape => shape.Weight > 0).ToList();
        serializedObject.Update();
        var property = serializedObject.FindProperty(nameof(StyleComponent.BlendShapeAnimations));
        CustomEditorUtility.ClearAllElements(property);
        CustomEditorUtility.AddBlendShapeAnimations(property, blendShapes.ToBlendShapeAnimations().ToList());
        serializedObject.ApplyModifiedProperties();
    }

    // 子にDefault Expressionがあるか確認する
    private bool HasDefault()
    {
        var defaultExpression = Component.transform.Find("Default");
        if (defaultExpression == null) return false;
        var defaultExpressionComponent = defaultExpression.GetComponent<FaceTuneComponent>();
        if (defaultExpressionComponent == null) return false;
        var hasConditions = defaultExpression.GetComponentsInChildren<ConditionComponent>(true)
            .Any(c => c.HandGestureConditions.Count > 0 || c.ParameterConditions.Count > 0);
        if (hasConditions) return false;
        return true;
    }

    private void AsDefault()
    {
        var defaultExpression = new GameObject("Default");
        defaultExpression.transform.parent = Component.transform;
        defaultExpression.transform.SetAsFirstSibling();

        var defaultExpressionComponent = defaultExpression.AddComponent<FaceTuneComponent>();
        defaultExpressionComponent.FacialSettings = new(TrackingPermission.Allow, TrackingPermission.Allow, false);

        Undo.RegisterCreatedObjectUndo(defaultExpression, "Create Default Expression");
        EditorGUIUtility.PingObject(defaultExpression);
    }

    [MenuItem($"CONTEXT/{nameof(StyleComponent)}/Apply to SkinnedMeshRenderer")]
    private static void ApplyToSkinnedMeshRenderer(MenuCommand command)
    {
        var component = command.context as StyleComponent;
        if (component == null) throw new InvalidOperationException("FacialStyleComponent not found");
        if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context)) throw new InvalidOperationException("Context not found");
        var blendShapeSet = new BlendShapeWeightSet();
        ExpressionDataUtility.AddFirstFrameBlendShapes(component.Data, blendShapeSet, string.Empty);
        var faceRenderer = context.FaceRenderer;
        var faceMesh = context.FaceMesh;
        Undo.RecordObject(faceRenderer, "Apply Blend Shape");
        faceRenderer.ApplyBlendShapes(faceMesh, blendShapeSet, 0f); // FacialStyleの挙動を踏襲し未指定は0で上書き
        Selection.activeGameObject = faceRenderer.gameObject;
        EditorGUIUtility.PingObject(faceRenderer);
    }
}