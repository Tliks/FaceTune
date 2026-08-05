namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(StyleComponent))]
internal sealed class StyleComponentEditor : FaceTuneSectionEditorBase<StyleComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateExpressionSection(), CreateOtherSection() };

    private FaceTuneSection CreateExpressionSection()
        => CreateSection(
            "style.expression.section.label",
            new ExpressionSectionDrawer(serializedObject, Component, targets.Length, CreateExpressionOptions),
            defaultExpanded: true,
            populateHeaderMenu: PopulateExpressionHeaderMenu);

    private FaceTuneSection CreateOtherSection()
        => CreateSection(
            "expression.group.other.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(StyleComponent.ApplyToRenderer)),
                    false,
                    "style.applyToRenderer.label")),
            defaultExpanded: false);

    private ExpressionGUIOptions CreateExpressionOptions()
        => new(ExternalSourceLabel: "style.otherStyle.label".LG());

    private void PopulateExpressionHeaderMenu(GenericMenu menu)
    {
        menu.AddItem("style.getFromRenderer.button".LG(), false, () => GetFromRenderer());
        menu.AddItem("style.applyToRenderer.menu".LG(), false, () => ApplyToSkinnedMeshRenderer());
    }

    private void GetFromRenderer()
    {
        if (targets.Length != 1 || !AvatarContext.TryGet(Component.gameObject, out var context, out _)) return;
        var animations = context.FaceRenderer
            .GetBlendShapeWeights(context.FaceMesh)
            .Where(shape => shape.Weight != 0f)
            .ToBlendShapeAnimations()
            .ToArray();
        serializedObject.UpdateIfRequiredOrScript();
        var property = serializedObject
            .FindProperty(nameof(StyleComponent.Data))
            .FindPropertyRelative(nameof(ExpressionData.BlendShapeAnimations));
        ExpressionGUI.SetBlendShapeAnimations(property, animations);
        serializedObject.ApplyModifiedProperties();
    }

    private void ApplyToSkinnedMeshRenderer()
    {
        if (!AvatarContext.TryGet(Component.gameObject, out var context, out _)) return;

        var animations = new List<BlendShapeWeightAnimation>();
        Component.GetAnimations(animations, context.BodyPath, includeStyleSources: true);
        var set = new BlendShapeWeightSet(animations.ToFirstFrameBlendShapes());
        Undo.RecordObject(context.FaceRenderer, "Apply Blend Shape");
        context.FaceRenderer.ApplyBlendShapes(context.FaceMesh, set, 0f);
        Selection.activeGameObject = context.FaceRenderer.gameObject;
        EditorGUIUtility.PingObject(context.FaceRenderer);
    }
}
