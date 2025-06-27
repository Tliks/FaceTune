namespace com.aoyon.facetune.ui;

internal static class CustomEditorUtility
{
    public static bool TryGetContext(GameObject obj, [NotNullWhen(true)] out SessionContext? context)
    {
        return SessionContextBuilder.TryBuild(obj, out context);
    }

    public static void ToClip(string relativePath, IEnumerable<BlendShape> blendShapes)
    {
        var clip = new AnimationClip();
        clip.SetBlendShapes(relativePath, blendShapes);
        var path = EditorUtility.SaveFilePanelInProject("Save FacialExpression as Clip", "FacialExpression", "anim", "Please enter the name of the animation clip.");
        if (string.IsNullOrEmpty(path)) return;
        AssetDatabase.CreateAsset(clip, path);
    }

    public static void ToClip(List<GenericAnimation> genericAnimations)
    {
        var clip = new AnimationClip();
        clip.SetGenericAnimations(genericAnimations);
        var path = EditorUtility.SaveFilePanelInProject("Save GenericAnimations as Clip", "GenericAnimations", "anim", "Please enter the name of the animation clip.");
        if (string.IsNullOrEmpty(path)) return;
        AssetDatabase.CreateAsset(clip, path);
    }
}