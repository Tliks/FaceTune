namespace aoyon.facetune.ui;

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

    public static void OpenEditor(GameObject obj, Action<BlendShapeSet> onApply)
    {
        if (!TryGetContext(obj, out var context)) return;
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, new(context.ZeroWeightBlendShapes), new());
        if (window == null) return;
        window.RegisterApplyCallback(onApply);
    }

    public static void ClearAnimations(SerializedProperty property)
    {
        property.arraySize = 0;
    }

    public static void AddShapesAsSingleFrame(SerializedProperty property, IReadOnlyList<BlendShape> newShapes, bool clear = false)
    {
        var animations = newShapes.Select(shape => BlendShapeAnimation.SingleFrame(shape.Name, shape.Weight)).ToList();
        if (clear)
        {
            ClearAnimations(property);
        }
        property.arraySize = animations.Count;
        for (var i = 0; i < animations.Count; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            var nameProp = element.FindPropertyRelative(BlendShapeAnimation.NamePropName);
            var curveProp = element.FindPropertyRelative(BlendShapeAnimation.CurvePropName);
            nameProp.stringValue = animations[i].Name;
            curveProp.animationCurveValue = animations[i].Curve;
        }
    }

    public static void AddAnimations(SerializedProperty property, IReadOnlyList<BlendShapeAnimation> newAnimations, bool clear = false)
    {
        if (clear)
        {
            ClearAnimations(property);
        }
        property.arraySize = newAnimations.Count;
        for (var i = 0; i < newAnimations.Count; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(BlendShapeAnimation.NamePropName).stringValue = newAnimations[i].Name;
            element.FindPropertyRelative(BlendShapeAnimation.CurvePropName).animationCurveValue = newAnimations[i].Curve;
        }
    }
}