namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(StyleComponent))]
internal sealed class StyleComponentEditor : FaceTuneEditor<StyleComponent>
{
    private bool _expressionExpanded = true;
    private bool _otherExpressionExpanded;
    private bool _otherExpanded;

    private ExpressionGUIOptions ExpressionOptions => new(
        HeaderLabel: "style.expression.section.label".LG(),
        ExternalSourceLabel: "style.otherStyle.label".LG(),
        FooterButtonLabel: "style.getFromRenderer.button".LG(),
        FooterButtonAction: GetFromRenderer);

    private void OnEnable()
    {
        _otherExpressionExpanded = targets
            .Cast<StyleComponent>()
            .Any(component => ExpressionGUI.HasExternalSource(component, component.Data, component.DataReference));
    }

    protected override float GetInspectorHeight()
        => ExpressionGUI.GetHeight(
               serializedObject.FindProperty(nameof(StyleComponent.Data)),
               _expressionExpanded,
               _otherExpressionExpanded,
               ExpressionOptions)
         + GUIHelper.HeaderSpacing
         + SectionHeight(_otherExpanded, ContentHeight(1));

    protected override void DrawInspector(Rect position)
    {
        var expressionHeight = ExpressionGUI.GetHeight(
            serializedObject.FindProperty(nameof(StyleComponent.Data)),
            _expressionExpanded,
            _otherExpressionExpanded,
            ExpressionOptions);
        var expressionRect = new Rect(position.x, position.y, position.width, expressionHeight);
        ExpressionGUI.Draw(
            expressionRect,
            serializedObject,
            Component,
            targets.Length,
            ref _expressionExpanded,
            ref _otherExpressionExpanded,
            ExpressionOptions);

        position.y = expressionRect.yMax + GUIHelper.HeaderSpacing;
        position.height = SectionHeight(_otherExpanded, ContentHeight(1));
        var header = new Rect(position.x, position.y, position.width, GUIHelper.ShurikenHeaderHeight);
        _otherExpanded = GUIHelper.DrawShuriken(
            header,
            _otherExpanded,
            "expression.group.other.label".LG());
        if (_otherExpanded)
        {
            var region = new Rect(
                position.x,
                header.yMax + GUIHelper.ContentSpacing,
                position.width,
                ContentHeight(1));
            if (Event.current.type == EventType.Repaint) GUIHelper.DrawRegion(region);
            var content = new Rect(
                region.x + GUIHelper.ContentPadding,
                region.y + GUIHelper.ContentPadding,
                region.width - GUIHelper.ContentPadding * 2f,
                GUIHelper.LineHeight);
            GUIHelper.DrawToggleLeft(
                content,
                serializedObject.FindProperty(nameof(StyleComponent.ApplyToRenderer)),
                "style.applyToRenderer.label".LG());
        }
    }

    private static float ContentHeight(int rows)
        => GUIHelper.ContentPadding * 2f
         + GUIHelper.LineHeight * rows
         + GUIHelper.VerticalSpacing * (rows - 1);

    private static float SectionHeight(bool expanded, float contentHeight)
        => GUIHelper.ShurikenHeaderHeight
         + (expanded
             ? GUIHelper.ContentSpacing + GUIHelper.ContentBottomSpacing + contentHeight
             : 0f);

    private void GetFromRenderer()
    {
        if (targets.Length != 1 || !CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var animations = context.FaceRenderer
            .GetBlendShapeWeights(context.FaceMesh)
            .ToBlendShapeAnimations()
            .ToArray();
        var property = serializedObject
            .FindProperty(nameof(StyleComponent.Data))
            .FindPropertyRelative("BlendShapeAnimations");
        CustomEditorUtility.ClearAllElements(property);
        CustomEditorUtility.AddBlendShapeAnimations(property, animations);
    }

    [MenuItem($"CONTEXT/{nameof(StyleComponent)}/Apply to SkinnedMeshRenderer")]
    private static void ApplyToSkinnedMeshRenderer(MenuCommand command)
    {
        if (command.context is not StyleComponent component) return;
        if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context)) return;

        var set = new BlendShapeWeightSet();
        CustomEditorUtility.AddClipFirstFrame(component.Data, set, string.Empty);
        set.AddRange(component.Data.BlendShapeAnimations.Select(animation => animation.ToFirstFrameBlendShape()));
        Undo.RecordObject(context.FaceRenderer, "Apply Blend Shape");
        context.FaceRenderer.ApplyBlendShapes(context.FaceMesh, set, 0f);
        Selection.activeGameObject = context.FaceRenderer.gameObject;
        EditorGUIUtility.PingObject(context.FaceRenderer);
    }
}
