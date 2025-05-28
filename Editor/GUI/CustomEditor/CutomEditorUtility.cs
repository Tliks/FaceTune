namespace com.aoyon.facetune.ui;

internal static class CustomEditorUtility
{
    public static void ToClip(GameObject obj, Func<SessionContext, IEnumerable<BlendShape>?> blendShapesProvider)
    {
        if (!SessionContextBuilder.TryGet(obj, out var context)) return;
        var relativePath = HierarchyUtility.GetRelativePath(context.Root, obj)!;
        var blendShapes = blendShapesProvider(context);
        if (blendShapes == null) return;
        var clip = new AnimationClip();
        AnimationUtility.SetBlendShapesToClip(clip, relativePath, blendShapes);
        var path = EditorUtility.SaveFilePanelInProject("Save FacialExpression as Clip", "FacialExpression", "anim", "Please enter the name of the animation clip.");
        if (string.IsNullOrEmpty(path)) return;
        AssetDatabase.CreateAsset(clip, path);
    }
}