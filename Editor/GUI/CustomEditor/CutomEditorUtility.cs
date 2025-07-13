using aoyon.facetune.ui.shapes_editor;

namespace aoyon.facetune.ui;

internal static class CustomEditorUtility
{
    public static bool TryGetContext(GameObject obj, [NotNullWhen(true)] out SessionContext? context)
    {
        if (SessionContextBuilder.TryBuild(obj, out context, out var result))
        {
            return true;
        }
        Debug.LogError($"Failed to get context: {result}");
        return false;
    }

    public static void SaveAsClip(Action<AnimationClip> editClip)
    {
        var clip = new AnimationClip();
        editClip(clip);
        var path = EditorUtility.SaveFilePanelInProject("Save as a Clip", "", "anim", "Please enter the name of the animation clip.");
        if (string.IsNullOrEmpty(path)) throw new Exception("path is empty");
        AssetDatabase.CreateAsset(clip, path);
    }

    public static void OpenEditorAndApplyBlendShapeSet(Component component, BlendShapeSet defaultOverrides, Func<SerializedObject, SerializedProperty> getProperty, BlendShapeSet? facialStyleSet = null)
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
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, new(context.ZeroWeightBlendShapes.Select(x => x.Name)), defaultOverrides, facialStyleSet);
        if (window == null) return;
        window.RegisterApplyCallback(onApply);
    }

    public static void OpenEditorAndApplyBlendShapeNames(Component component, BlendShapeSet defaultOverrides, Func<SerializedObject, SerializedProperty> getProperty, BlendShapeSet? facialStyleSet = null)
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
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, new(context.ZeroWeightBlendShapes.Select(x => x.Name)), defaultOverrides, facialStyleSet);
        if (window == null) return;
        window.RegisterApplyCallback(onApply);
    }

    public static void OpenEditor(GameObject obj, BlendShapeSet defaultOverrides, Action<BlendShapeSet> onApply, BlendShapeSet? facialStyleSet = null)
    {
        if (!TryGetContext(obj, out var context)) return;
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, new(context.ZeroWeightBlendShapes.Select(x => x.Name)), defaultOverrides, facialStyleSet);
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

    public static void AddBlendShapeAnimations(SerializedProperty blendShapeAnimation, IReadOnlyList<BlendShapeAnimation> newAnimations)
    {
        blendShapeAnimation.arraySize = newAnimations.Count;
        for (var i = 0; i < newAnimations.Count; i++)
        {
            var element = blendShapeAnimation.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(BlendShapeAnimation.NamePropName).stringValue = newAnimations[i].Name;
            element.FindPropertyRelative(BlendShapeAnimation.CurvePropName).animationCurveValue = newAnimations[i].Curve;
        }
    }

    public static void AddGenericAnimations(SerializedProperty genericAnimations, IReadOnlyList<GenericAnimation> newAnimations)
    {
        genericAnimations.arraySize = newAnimations.Count;
        for (var i = 0; i < newAnimations.Count; i++)
        {
            var element = genericAnimations.GetArrayElementAtIndex(i);
            var curveBindingProp = element.FindPropertyRelative(GenericAnimation.CurveBindingPropName);
            var curveProp = element.FindPropertyRelative(GenericAnimation.CurvePropName);
            var objectReferenceCurveProp = element.FindPropertyRelative(GenericAnimation.ObjectReferenceCurvePropName);
            
            // SerializableCurveBindingの各プロパティを設定
            var newCurveBinding = newAnimations[i].CurveBinding;
            curveBindingProp.FindPropertyRelative("path").stringValue = newCurveBinding.Path;
            curveBindingProp.FindPropertyRelative("type").stringValue = newCurveBinding.Type?.AssemblyQualifiedName ?? "";
            curveBindingProp.FindPropertyRelative("propertyName").stringValue = newCurveBinding.PropertyName;
            
            // AnimationCurveを設定
            curveProp.animationCurveValue = newAnimations[i].Curve;
            
            // ObjectReferenceCurveを設定
            var newObjectReferenceCurve = newAnimations[i].ObjectReferenceCurve;
            objectReferenceCurveProp.arraySize = newObjectReferenceCurve.Count;
            for (var j = 0; j < newObjectReferenceCurve.Count; j++)
            {
                var keyframeElement = objectReferenceCurveProp.GetArrayElementAtIndex(j);
                keyframeElement.FindPropertyRelative("time").floatValue = newObjectReferenceCurve[j].Time;
                keyframeElement.FindPropertyRelative("value").objectReferenceValue = newObjectReferenceCurve[j].Value;
            }
        }
    }
}