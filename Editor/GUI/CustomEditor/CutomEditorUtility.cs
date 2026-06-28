using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

internal static class CustomEditorUtility
{
    public static bool TryGetContext(GameObject obj, [NotNullWhen(true)] out AvatarContext? context)
    {
        if (AvatarContextBuilder.TryBuild(obj, out context, out var result))
        {
            return true;
        }
        LocalizedLog.Error("Log:error:AvatarContextBuilder:FailedToBuild", result.ToString());
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

    public static void OpenEditor(GameObject obj, IShapesEditorTargeting targeting, IReadOnlyBlendShapeSet? defaultOverrides = null, IReadOnlyBlendShapeSet? styleSet = null, IReadOnlyBlendShapeSet? baseSet = null)
    {
        if (!TryGetContext(obj, out var context)) return;
        var window = FacialShapesEditor.TryOpenEditor(context.FaceRenderer, targeting, defaultOverrides, styleSet, baseSet);
        if (window == null) return;
    }


    public static void ClearAllElements(Component component, Func<SerializedObject, SerializedProperty> getProperty)
    {
        ModicyComponent(component, so =>
        {
            var property = getProperty(so);
            ClearAllElements(property);
        });
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
    
    public static void AddBlendShapeAnimations(Component component, Func<SerializedObject, SerializedProperty> getProperty, IReadOnlyCollection<BlendShapeWeightAnimation> animations, bool preferLatter = true)
    {
        ModicyComponent(component, so =>
        {
            var property = getProperty(so);
            AddBlendShapeAnimations(property, animations, preferLatter);
        });
    }
    public static void AddBlendShapeAnimations(SerializedProperty blendShapeAnimation, IReadOnlyCollection<BlendShapeWeightAnimation> animations, bool preferLatter = true)
    {
        // 既存のアニメーションを辞書化
        var existingAnimations = new Dictionary<string, (SerializedProperty, AnimationCurve)>();
        for (int i = 0; i < blendShapeAnimation.arraySize; i++)
        {
            var element = blendShapeAnimation.GetArrayElementAtIndex(i);
            var name = element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue;
            var curve = element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue;
            if (!string.IsNullOrEmpty(name))
            {
                existingAnimations[name] = (element, curve);
            }
        }

        var newAnimations = animations.ToList();

        foreach (var anim in newAnimations)
        {
            if (existingAnimations.TryGetValue(anim.Name, out var existing))
            {
                bool isSame = anim.Curve.Equals(existing.Item2);
                if (isSame)
                {
                    // 同じなら何もしない（既存を維持）
                }
                else
                {
                    // 既に存在していて同値ではなく、かつpreferLatterの場合は上書き
                    if (preferLatter)
                    {
                        existing.Item1.animationCurveValue = anim.Curve;
                    }
                }
            }
            else // 新規追加
            {
                blendShapeAnimation.arraySize++;
                var newElement = blendShapeAnimation.GetArrayElementAtIndex(blendShapeAnimation.arraySize - 1);
                newElement.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue = anim.Name;
                newElement.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = anim.Curve;
            }
        }
    }

}