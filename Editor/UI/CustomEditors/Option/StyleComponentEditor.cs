namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(StyleComponent))]
internal sealed class StyleComponentEditor : FaceTuneSectionEditor<StyleComponent>
{
    private ExpressionGUIOptions ExpressionOptions => new(
        ExternalSourceLabel: "style.otherStyle.label".LG(),
        FooterButtonLabel: "style.getFromRenderer.button".LG(),
        FooterButtonAction: GetFromRenderer);

    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreateExpressionSection(),
            CreateOtherSection()
        };

    private FaceTuneSection CreateExpressionSection()
    {
        var data = serializedObject.FindProperty(nameof(StyleComponent.Data));
        ExpressionGUI.InitializeExpansions(data);
        return new(
            "style.expression.section.label".LG(),
            () => ExpressionGUI.GetContentHeight(data, ExpressionOptions),
            content => ExpressionGUI.DrawContent(
                content,
                serializedObject,
                Component,
                targets.Length,
                ExpressionOptions),
            true);
    }

    private FaceTuneSection CreateOtherSection()
        => new(
            "expression.group.other.label".LG(),
            () => GUIHelper.GetLinesHeight(1),
            content => GUIHelper.DrawToggleLeft(
                content,
                serializedObject.FindProperty(nameof(StyleComponent.ApplyToRenderer)),
                "style.applyToRenderer.label".LG()),
            false);

    private void GetFromRenderer()
    {
        if (targets.Length != 1 || !AvatarContext.TryGet(Component.gameObject, out var context, out _)) return;
        var animations = context.FaceRenderer
            .GetBlendShapeWeights(context.FaceMesh)
            .Where(shape => shape.Weight != 0f)
            .ToBlendShapeAnimations()
            .ToArray();
        var property = serializedObject
            .FindProperty(nameof(StyleComponent.Data))
            .FindPropertyRelative(nameof(ExpressionData.BlendShapeAnimations));
        ExpressionGUI.SetBlendShapeAnimations(property, animations);
    }

    [MenuItem($"CONTEXT/{nameof(StyleComponent)}/Apply to SkinnedMeshRenderer")]
    private static void ApplyToSkinnedMeshRenderer(MenuCommand command)
    {
        if (command.context is not StyleComponent component) return;
        if (!AvatarContext.TryGet(component.gameObject, out var context, out _)) return;

        var animations = new List<BlendShapeWeightAnimation>();
        component.GetAnimations(animations, context.BodyPath, includeStyleSources: true);
        var set = new BlendShapeWeightSet(animations.ToFirstFrameBlendShapes());
        Undo.RecordObject(context.FaceRenderer, "Apply Blend Shape");
        context.FaceRenderer.ApplyBlendShapes(context.FaceMesh, set, 0f);
        Selection.activeGameObject = context.FaceRenderer.gameObject;
        EditorGUIUtility.PingObject(context.FaceRenderer);
    }
}
