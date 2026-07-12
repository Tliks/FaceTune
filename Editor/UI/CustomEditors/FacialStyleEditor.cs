namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(StyleComponent))]
internal sealed class FacialStyleEditor : FaceTuneEditor<StyleComponent>
{
    protected override void DrawInspector()
    {
        DrawProperty(nameof(StyleComponent.DataReference));
        DrawProperty(nameof(StyleComponent.Data));
        DrawProperty(nameof(StyleComponent.ApplyToRenderer));
    }

    [MenuItem($"CONTEXT/{nameof(StyleComponent)}/Apply to SkinnedMeshRenderer")]
    private static void ApplyToSkinnedMeshRenderer(MenuCommand command)
    {
        if (command.context is not StyleComponent component) return;
        if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context)) return;

        var set = new BlendShapeWeightSet();
        ExpressionDataEditorUtility.AddClipFirstFrame(component.Data, set, string.Empty);
        set.AddRange(component.Data.BlendShapeAnimations.Select(animation => animation.ToFirstFrameBlendShape()));
        Undo.RecordObject(context.FaceRenderer, "Apply Blend Shape");
        context.FaceRenderer.ApplyBlendShapes(context.FaceMesh, set, 0f);
        Selection.activeGameObject = context.FaceRenderer.gameObject;
        EditorGUIUtility.PingObject(context.FaceRenderer);
    }
}
