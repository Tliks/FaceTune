using Aoyon.FaceTune.Gui;
using nadena.dev.ndmf.runtime;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal abstract class IShapesEditorTargeting
{
    public abstract Object? GetTarget();
    public abstract Type GetObjectType();
    public abstract void SetTarget(Object? target);
    public event Action? OnTargetChanged;
    protected void RaiseTargetChanged() => OnTargetChanged?.Invoke();
    public abstract void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager);
    public abstract VisualElement? DrawOptions();
}

internal abstract class IShapesEditorTargeting<T> : IShapesEditorTargeting where T : Object
{
    public abstract T? Target { get; set; }
    public override Object? GetTarget() => Target;
    public override Type GetObjectType() => typeof(T);
    public override void SetTarget(Object? target)
    {
        Target = target as T;
        RaiseTargetChanged();
    }
    public override VisualElement? DrawOptions() => null;
}

internal sealed class AnimationClipTargeting : IShapesEditorTargeting<AnimationClip>
{
    public override AnimationClip? Target { get; set; }
    public bool ZeroUnspecifiedBlendShapes { get; set; } = true;
    public bool ZeroUnavailableBlendShapes { get; set; } = true;

    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new InvalidOperationException("Target is not set.");
        var path = RuntimeUtil.RelativePath(root, renderer.gameObject)
            ?? throw new InvalidOperationException("Renderer is outside avatar root.");
        var prefix = FaceTuneConstants.BlendShapePropertyPrefix;
        var originalNames = new HashSet<string>(StringComparer.Ordinal);
        var multiFrameNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in AnimationUtility.GetCurveBindings(Target))
        {
            if (binding.path != path || binding.type != typeof(SkinnedMeshRenderer)
                || !binding.propertyName.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var name = binding.propertyName[prefix.Length..];
            originalNames.Add(name);
            var curve = AnimationUtility.GetEditorCurve(Target, binding);
            if (curve != null && curve.keys.Length > 1)
            {
                multiFrameNames.Add(name);
                continue;
            }
            if (!dataManager.IsExplicitlyExcluded(name) || ZeroUnavailableBlendShapes)
                AnimationUtility.SetEditorCurve(Target, binding, null);
        }

        var targetValues = new BlendShapeWeightSet();
        dataManager.GetTargetValues(targetValues);
        var rendererNames = renderer.sharedMesh.GetBlendShapeNames().ToHashSet(StringComparer.Ordinal);
        if (ZeroUnspecifiedBlendShapes)
        {
            var zeroNames = originalNames.Count != 0 ? originalNames : rendererNames;
            foreach (var name in zeroNames)
            {
                if (!targetValues.ContainsKey(name)
                    && !multiFrameNames.Contains(name)
                    && (!dataManager.IsExplicitlyExcluded(name) || ZeroUnavailableBlendShapes))
                    targetValues.Add(new BlendShapeWeight(name, 0f));
            }
        }
        if (ZeroUnavailableBlendShapes)
        {
            foreach (var name in dataManager.ExplicitlyExcluded)
            {
                if (rendererNames.Contains(name)
                    && !multiFrameNames.Contains(name)
                    && !targetValues.ContainsKey(name))
                    targetValues.Add(new BlendShapeWeight(name, 0f));
            }
        }

        Target.AddBlendShapeAnimations(
            path,
            targetValues
                .Where(value => !multiFrameNames.Contains(value.Name))
                .ToBlendShapeAnimations());
        Target.SaveChanges();
    }

    public override VisualElement DrawOptions()
    {
        var menu = new ToolbarMenu { text = "facialEditor.clipOptions.label".LS() };
        menu.menu.AppendAction(
            "facialEditor.zeroUnspecifiedBlendShapes.option".LS(),
            _ => ZeroUnspecifiedBlendShapes = !ZeroUnspecifiedBlendShapes,
            _ => ZeroUnspecifiedBlendShapes
                ? DropdownMenuAction.Status.Checked
                : DropdownMenuAction.Status.Normal);
        menu.menu.AppendAction(
            "facialEditor.zeroUnavailableBlendShapes.option".LS(),
            _ => ZeroUnavailableBlendShapes = !ZeroUnavailableBlendShapes,
            _ => ZeroUnavailableBlendShapes
                ? DropdownMenuAction.Status.Checked
                : DropdownMenuAction.Status.Normal);
        return menu;
    }
}

internal abstract class FacialSourceTargeting<T> : IShapesEditorTargeting<T> where T : Component
{
    protected abstract string SourcePropertyName { get; }
    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new InvalidOperationException("Target is not set.");
        var serialized = new SerializedObject(Target);
        serialized.Update();
        var animations = serialized.FindProperty(SourcePropertyName)
            .FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        FacialShapeAnimationSaver.Save(animations, dataManager);
        serialized.ApplyModifiedProperties();
    }
}

internal static class FacialShapeAnimationSaver
{
    internal static void Save(
        SerializedProperty animations,
        BlendShapeOverrideManager dataManager)
    {
        var originalAnimations = ReadAnimations(animations).ToArray();
        var targetValues = new BlendShapeWeightSet();
        dataManager.GetTargetValues(targetValues);

        var preservedAnimations = originalAnimations
            .Where(animation => animation.IsMultiFrame
                             || dataManager.IsExplicitlyExcluded(animation.Name))
            .ToArray();
        var preservedNames = preservedAnimations
            .Select(animation => animation.Name)
            .ToHashSet(StringComparer.Ordinal);

        animations.SynchronizeArrayByKey(
            preservedAnimations.Concat(targetValues
                .Where(value => !preservedNames.Contains(value.Name))
                .ToBlendShapeAnimations()),
            element => element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue,
            animation => animation.Name,
            (element, animation) => element.CopyFrom(animation),
            overwrite: true);
    }

    private static IEnumerable<BlendShapeWeightAnimation> ReadAnimations(SerializedProperty property)
    {
        for (var index = 0; index < property.arraySize; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            yield return new BlendShapeWeightAnimation(
                element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue,
                element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName)
                    .animationCurveValue);
        }
    }
}

internal sealed class ExpressionDataTargeting : FacialSourceTargeting<ExpressionDataComponent>
{
    public override ExpressionDataComponent? Target { get; set; }
    protected override string SourcePropertyName => nameof(ExpressionDataComponent.FacialBlendShapes);
}

internal sealed class FaceTuneDataTargeting : FacialSourceTargeting<ExpressionComponent>
{
    public override ExpressionComponent? Target { get; set; }
    protected override string SourcePropertyName => nameof(ExpressionComponent.FacialBlendShapes);
}

internal sealed class SettingsFacialTargeting : FacialSourceTargeting<SettingsComponent>
{
    public override SettingsComponent? Target { get; set; }
    protected override string SourcePropertyName => nameof(SettingsComponent.FacialBlendShapes);
}
