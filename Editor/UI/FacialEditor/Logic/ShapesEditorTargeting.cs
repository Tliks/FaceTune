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
    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new InvalidOperationException("Target is not set.");
        var targetValues = new BlendShapeWeightSet();
        dataManager.GetTargetValues(targetValues);
        var path = RuntimeUtil.RelativePath(root, renderer.gameObject) ?? throw new InvalidOperationException("Renderer is outside avatar root.");
        foreach (var binding in AnimationUtility.GetCurveBindings(Target))
        {
            var prefix = FaceTuneConstants.BlendShapePropertyPrefix;
            if (binding.path != path || binding.type != typeof(SkinnedMeshRenderer)
                || !binding.propertyName.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var name = binding.propertyName[prefix.Length..];
            if (!dataManager.IsExplicitlyExcluded(name))
                AnimationUtility.SetEditorCurve(Target, binding, null);
        }
        Target.AddBlendShapeAnimations(path, targetValues.ToBlendShapeAnimations());
        Target.SaveChanges();
    }
}

internal abstract class FacialSourceTargeting<T> : IShapesEditorTargeting<T> where T : Component
{
    protected abstract string SourcePropertyName { get; }
    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new InvalidOperationException("Target is not set.");
        var targetValues = new BlendShapeWeightSet();
        dataManager.GetTargetValues(targetValues);
        var serialized = new SerializedObject(Target);
        serialized.Update();
        var animations = serialized.FindProperty(SourcePropertyName)
            .FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        var values = new List<BlendShapeWeightAnimation>();
        for (var index = 0; index < animations.arraySize; index++)
        {
            var element = animations.GetArrayElementAtIndex(index);
            var name = element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue;
            if (!dataManager.IsExplicitlyExcluded(name)) continue;
            values.Add(new BlendShapeWeightAnimation(
                name,
                element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName)
                    .animationCurveValue));
        }
        values.AddRange(targetValues.ToBlendShapeAnimations());
        FacialDataGUI.SetBlendShapeAnimations(animations, values);
        serialized.ApplyModifiedProperties();
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
