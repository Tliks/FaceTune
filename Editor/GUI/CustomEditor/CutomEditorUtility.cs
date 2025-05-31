namespace com.aoyon.facetune.ui;

internal static class CustomEditorUtility
{
    public static bool TryGetContext(GameObject obj, [NotNullWhen(true)] out SessionContext? context)
    {
        return SessionContextBuilder.TryBuild(obj, out context);
    }

    public static void ToClip(GameObject obj, Func<DefaultExpressionContext , IEnumerable<BlendShape>?> blendShapesProvider)
    {
        if (!TryGetContext(obj, out var context)) return;

        var relativePath = HierarchyUtility.GetRelativePath(context.Root, context.FaceRenderer.gameObject)!;
        var blendShapes = blendShapesProvider(context.DEC);
        if (blendShapes == null) return;

        var clip = new AnimationClip();
        clip.SetBlendShapes(relativePath, blendShapes);
        var path = EditorUtility.SaveFilePanelInProject("Save FacialExpression as Clip", "FacialExpression", "anim", "Please enter the name of the animation clip.");
        if (string.IsNullOrEmpty(path)) return;
        AssetDatabase.CreateAsset(clip, path);
    }
}