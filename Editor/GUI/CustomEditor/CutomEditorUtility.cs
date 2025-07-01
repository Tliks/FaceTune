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

    public static void OpenEditorAndApplyBlendShapeSet(Component component, Func<SerializedObject, SerializedProperty> getProperty)
    {
        var onApply = new Action<BlendShapeSet>(set =>
        {
            var so = new SerializedObject(component);
            so.Update();
            var property = getProperty(so);
            property.serializedObject.Update();
            AddShapesAsSingleFrame(property, set.BlendShapes.ToList());
            property.serializedObject.ApplyModifiedProperties();
        });

        if (!TryGetContext(component.gameObject, out var context)) return;
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, new(context.ZeroWeightBlendShapes), new());
        if (window == null) return;
        window.RegisterApplyCallback(onApply);
    }

    public static void OpenEditorAndApplyBlendShapeNames(Component component, Func<SerializedObject, SerializedProperty> getProperty)
    {
        var onApply = new Action<BlendShapeSet>(set =>
        {
            var so = new SerializedObject(component);
            so.Update();
            var property = getProperty(so);
            property.serializedObject.Update();
            AddShapesAsNames(property, set.BlendShapes.Select(shape => shape.Name).ToList());
            property.serializedObject.ApplyModifiedProperties();
        });

        if (!TryGetContext(component.gameObject, out var context)) return;
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, new(context.ZeroWeightBlendShapes), new());
        if (window == null) return;
        window.RegisterApplyCallback(onApply);
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

    public static void AddShapesAsNames(SerializedProperty namesCollection, IReadOnlyList<string> names)
    {
        namesCollection.arraySize = names.Count;
        for (var i = 0; i < names.Count; i++)
        {
            var element = namesCollection.GetArrayElementAtIndex(i);
            element.stringValue = names[i];
        }
    }

    public static void AddShapesAsSingleFrame(SerializedProperty blendShapeAnimation, IReadOnlyList<BlendShape> newShapes)
    {
        var animations = newShapes.Select(shape => BlendShapeAnimation.SingleFrame(shape.Name, shape.Weight)).ToList();
        blendShapeAnimation.arraySize = animations.Count;
        for (var i = 0; i < animations.Count; i++)
        {
            var element = blendShapeAnimation.GetArrayElementAtIndex(i);
            var nameProp = element.FindPropertyRelative(BlendShapeAnimation.NamePropName);
            var curveProp = element.FindPropertyRelative(BlendShapeAnimation.CurvePropName);
            nameProp.stringValue = animations[i].Name;
            curveProp.animationCurveValue = animations[i].Curve;
        }
    }

    public static void AddAnimations(SerializedProperty blendShapeAnimation, IReadOnlyList<BlendShapeAnimation> newAnimations)
    {
        blendShapeAnimation.arraySize = newAnimations.Count;
        for (var i = 0; i < newAnimations.Count; i++)
        {
            var element = blendShapeAnimation.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(BlendShapeAnimation.NamePropName).stringValue = newAnimations[i].Name;
            element.FindPropertyRelative(BlendShapeAnimation.CurvePropName).animationCurveValue = newAnimations[i].Curve;
        }
    }
}