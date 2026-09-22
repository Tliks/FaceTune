using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal static class FacialShapeSaver
{
    public static void Save(
        Object target,
        string? animationPropertyPath,
        GameObject root,
        SkinnedMeshRenderer renderer,
        BlendShapeOverrideManager dataManager,
        bool zeroUnspecifiedBlendShapes,
        bool zeroUnavailableBlendShapes)
    {
        if (target is AnimationClip clip)
        {
            SaveClip(
                clip,
                root,
                renderer,
                dataManager,
                zeroUnspecifiedBlendShapes,
                zeroUnavailableBlendShapes);
            return;
        }

        if (target is not Component component || animationPropertyPath == null)
            throw new InvalidOperationException("The facial shape target is invalid.");

        using var serialized = new SerializedObject(component);
        serialized.Update();
        ShapeListSerialization.Save(
            serialized.FindProperty(animationPropertyPath),
            dataManager,
            animations: true);
        serialized.ApplyModifiedProperties();
    }

    private static void SaveClip(
        AnimationClip clip,
        GameObject root,
        SkinnedMeshRenderer renderer,
        BlendShapeOverrideManager dataManager,
        bool zeroUnspecifiedBlendShapes,
        bool zeroUnavailableBlendShapes)
    {
        var path = RuntimeUtil.RelativePath(root, renderer.gameObject)
            ?? throw new InvalidOperationException("Renderer is outside avatar root.");
        var prefix = FaceTuneConstants.BlendShapePropertyPrefix;
        var originalNames = new HashSet<string>(StringComparer.Ordinal);
        var protectedMultiFrame = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path != path || binding.type != typeof(SkinnedMeshRenderer)
                || !binding.propertyName.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var name = binding.propertyName[prefix.Length..];
            originalNames.Add(name);
            if (dataManager.Manages(name))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
                continue;
            }

            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null && curve.keys.Length > 1)
            {
                protectedMultiFrame.Add(name);
                continue;
            }
            if (!dataManager.IsExplicitlyExcluded(name) || zeroUnavailableBlendShapes)
                AnimationUtility.SetEditorCurve(clip, binding, null);
        }

        var targetAnimations = new List<BlendShapeWeightAnimation>();
        dataManager.GetTargetAnimations(targetAnimations);
        var targetNames = targetAnimations
            .Select(animation => animation.Name)
            .ToHashSet(StringComparer.Ordinal);
        var rendererNames = renderer.sharedMesh.GetBlendShapeNames().ToHashSet(StringComparer.Ordinal);
        if (zeroUnspecifiedBlendShapes)
        {
            var zeroNames = originalNames.Count != 0 ? originalNames : rendererNames;
            foreach (var name in zeroNames)
            {
                if (!targetNames.Contains(name)
                    && !protectedMultiFrame.Contains(name)
                    && (!dataManager.IsExplicitlyExcluded(name) || zeroUnavailableBlendShapes))
                    targetAnimations.Add(BlendShapeWeightAnimation.SingleFrame(name, 0f));
            }
        }
        if (zeroUnavailableBlendShapes)
        {
            foreach (var name in dataManager.ExplicitlyExcluded)
            {
                if (rendererNames.Contains(name)
                    && !targetNames.Contains(name)
                    && !protectedMultiFrame.Contains(name))
                    targetAnimations.Add(BlendShapeWeightAnimation.SingleFrame(name, 0f));
            }
        }

        clip.AddBlendShapeAnimations(path, targetAnimations);
        clip.SaveChanges();
    }
}

internal static class ShapeListSerialization
{
    internal static List<BlendShapeWeightAnimation> Read(
        SerializedProperty property,
        bool animations)
    {
        var result = new List<BlendShapeWeightAnimation>(property.arraySize);
        for (var index = 0; index < property.arraySize; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            var name = element.FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue;
            result.Add(animations
                ? new BlendShapeWeightAnimation(
                    name,
                    element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName)
                        .animationCurveValue)
                : BlendShapeWeightAnimation.SingleFrame(
                    name,
                    element.FindPropertyRelative(BlendShapeWeight.WeightPropName).floatValue));
        }
        return result;
    }

    internal static void Save(
        SerializedProperty property,
        BlendShapeOverrideManager dataManager,
        bool animations)
    {
        if (!animations)
        {
            var original = Read(property, false);
            var target = new BlendShapeWeightSet();
            dataManager.GetTargetValues(target);
            var values = original
                .Select(animation => animation.ToFirstFrameBlendShape())
                .Where(shape => !dataManager.Manages(shape.Name))
                .Concat(target);
            property.SynchronizeArrayByKey(
                values,
                element => element.FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue,
                shape => shape.Name,
                (element, shape) => element.CopyFrom(shape),
                overwrite: true);
            return;
        }

        var originalAnimations = Read(property, true);
        var targetAnimations = new List<BlendShapeWeightAnimation>();
        dataManager.GetTargetAnimations(targetAnimations);

        var preservedAnimations = originalAnimations
            .Where(animation => !dataManager.Manages(animation.Name));

        property.SynchronizeArrayByKey(
            preservedAnimations.Concat(targetAnimations),
            element => element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue,
            animation => animation.Name,
            (element, animation) => element.CopyFrom(animation),
            overwrite: true);
    }
}
