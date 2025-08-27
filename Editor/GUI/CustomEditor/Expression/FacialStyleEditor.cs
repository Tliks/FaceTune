using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FacialStyleComponent))]
internal class FacialStyleEditor : FaceTuneIMGUIEditorBase<FacialStyleComponent>
{
    private bool _hasDefault = false;
    public override void OnEnable()
    {
        base.OnEnable();
        _hasDefault = HasDefault();
    }

    protected override void OnInnerInspectorGUI()
    {
        DrawDefaultInspector(true);
        if (GUILayout.Button(Localization.G("FacialStyleComponent:UpdateFromScene")))
        {
            UpdateFromScene();
        }
        if (GUILayout.Button(Localization.G("FacialStyleComponent:OpenEditor")))
        {
            OpenEditor();
        }
        GUI.enabled = !_hasDefault;
        if (GUILayout.Button(Localization.G("FacialStyleComponent:AsDefault")))
        {
            AsDefault();
        }
        GUI.enabled = true;
    }

    private void OpenEditor()
    {
        var defaultOverride = new BlendShapeSet();
        Component.GetBlendShapes(defaultOverride);
        CustomEditorUtility.OpenEditor(Component.gameObject, new FacialStyleTargeting(){ Target = Component }, defaultOverride, null);
    }

    private void UpdateFromScene()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var blendShapes = context.FaceRenderer.GetBlendShapes(context.FaceMesh).Where(shape => shape.Weight > 0).ToList();
        serializedObject.Update();
        var property = serializedObject.FindProperty(nameof(FacialStyleComponent.BlendShapeAnimations));
        CustomEditorUtility.ClearAllElements(property);
        CustomEditorUtility.AddBlendShapeAnimations(property, blendShapes.ToBlendShapeAnimations().ToList());
        serializedObject.ApplyModifiedProperties();
    }

    // 子にDefault Expressionがあるか確認する
    private bool HasDefault()
    {
        var defaultExpression = Component.transform.Find("Default");
        if (defaultExpression == null) return false;
        var defaultExpressionComponent = defaultExpression.GetComponent<ExpressionComponent>();
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

        var defaultExpressionComponent = defaultExpression.AddComponent<ExpressionComponent>();
        defaultExpressionComponent.FacialSettings = new(TrackingPermission.Allow, TrackingPermission.Allow, false);

        Undo.RegisterCreatedObjectUndo(defaultExpression, "Create Default Expression");
        EditorGUIUtility.PingObject(defaultExpression);
    }

    [MenuItem($"CONTEXT/{nameof(FacialStyleComponent)}/Apply to SkinnedMeshRenderer")]
    private static void ApplyToSkinnedMeshRenderer(MenuCommand command)
    {
        var component = command.context as FacialStyleComponent;
        if (component == null) throw new InvalidOperationException("FacialStyleComponent not found");
        if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context)) throw new InvalidOperationException("Context not found");
        var blendShapeSet = new BlendShapeSet();
        component.GetBlendShapes(blendShapeSet);
        var faceRenderer = context.FaceRenderer;
        var faceMesh = context.FaceMesh;
        Undo.RecordObject(faceRenderer, "Apply Blend Shape");
        faceRenderer.ApplyBlendShapes(faceMesh, blendShapeSet, 0f); // FacialStyleの挙動を踏襲し未指定は0で上書き
        Selection.activeGameObject = faceRenderer.gameObject;
        EditorGUIUtility.PingObject(faceRenderer);
    }
}