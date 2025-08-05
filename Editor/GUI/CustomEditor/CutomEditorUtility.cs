using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

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

    public static void OpenEditor(GameObject obj, IShapesEditorTargeting targeting, IReadOnlyBlendShapeSet? defaultOverrides = null, IReadOnlyBlendShapeSet? facialStyleSet = null)
    {
        if (!TryGetContext(obj, out var context)) return;
        var window = FacialShapesEditor.TryOpenEditor(context.FaceRenderer, targeting, defaultOverrides, facialStyleSet);
        if (window == null) return;
    }

    public static void ClearAllElements(SerializedProperty property)
    {
        property.arraySize = 0;
    }

    private static void ModicyComponent(Component component, Action<SerializedObject> action)
    {
        var so = new SerializedObject(component);
        so.Update();
        action(so);
        so.ApplyModifiedProperties();
    }

    public static void AddShapesAsNames(Component component, Func<SerializedObject, SerializedProperty> getProperty, IReadOnlyCollection<string> names)
    {
        ModicyComponent(component, so =>
        {
            var property = getProperty(so);
            AddShapesAsNames(property, names);
        });
    }
    public static void AddShapesAsNames(SerializedProperty namesCollection, IReadOnlyCollection<string> names)
    {
        var newNames = names.ToList();
        namesCollection.arraySize = newNames.Count;
        for (var i = 0; i < newNames.Count; i++)
        {
            var element = namesCollection.GetArrayElementAtIndex(i);
            element.stringValue = newNames[i];
        }
    }
    
    public static void AddBlendShapeAnimations(Component component, Func<SerializedObject, SerializedProperty> getProperty, IReadOnlyCollection<BlendShapeWeightAnimation> animations)
    {
        ModicyComponent(component, so =>
        {
            var property = getProperty(so);
            AddBlendShapeAnimations(property, animations);
        });
    }
    public static void AddBlendShapeAnimations(SerializedProperty blendShapeAnimation, IReadOnlyCollection<BlendShapeWeightAnimation> animations)
    {
        var newAnimations = animations.ToList();
        blendShapeAnimation.arraySize = newAnimations.Count;
        for (var i = 0; i < newAnimations.Count; i++)
        {
            var element = blendShapeAnimation.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue = newAnimations[i].Name;
            element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = newAnimations[i].Curve;
        }
    }

    public static void AddGenericAnimations(Component component, Func<SerializedObject, SerializedProperty> getProperty, IReadOnlyCollection<GenericAnimation> animations)
    {
        ModicyComponent(component, so =>
        {
            var property = getProperty(so);
            AddGenericAnimations(property, animations);
        });
    }
    public static void AddGenericAnimations(SerializedProperty genericAnimations, IReadOnlyCollection<GenericAnimation> animations)
    {
        var newAnimations = animations.ToList();
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