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

    protected static void SetStringArray(
        Component target,
        Func<SerializedObject, SerializedProperty> getProperty,
        IReadOnlyCollection<string> values)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.Update();
        var property = getProperty(serializedObject);
        property.arraySize = values.Count;
        var index = 0;
        foreach (var value in values)
            property.GetArrayElementAtIndex(index++).stringValue = value;
        serializedObject.ApplyModifiedProperties();
    }

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
        if (Target == target) return;
        if (target == null)
        {
            Target = null;
        }
        else
        {
            Target = (T)target;
        }
        RaiseTargetChanged();
    }
    public override VisualElement? DrawOptions()
    {
        return null;
    }
}

internal class AnimationClipTargeting : IShapesEditorTargeting<AnimationClip>
{
    public override AnimationClip? Target { get; set; } = null;
    public bool AddZeroWeight { get; set; } = true;
    public bool AddBaseSet { get; set; } = true;
    public bool ExcludeTrackedShapes { get; set; } = true;

    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new Exception("Target is not set");
        var animations = new BlendShapeWeightAnimationSet();
        var path = RuntimeUtil.RelativePath(root, renderer.gameObject);
        if (path == null) throw new Exception("TargetRenderer is not a child of root");
        if (AddZeroWeight)
        {
            var zeroShapes = dataManager.AllKeys.Select(key => new BlendShapeWeight(key, 0f));
            animations.AddRange(zeroShapes.ToBlendShapeAnimations());
        }
        if (AddBaseSet)
        {
            animations.AddRange(dataManager.EffectiveBaseSet.ToBlendShapeAnimations());
        }
        var overrides = new BlendShapeWeightSet();
        dataManager.GetCurrentOverrides(overrides);
        animations.AddRange(overrides.ToBlendShapeAnimations());

        Target.RemoveAllCurveBindings();
        Target.AddBlendShapeAnimations(path, animations);
        Target.SaveChanges();
    }

    public override VisualElement? DrawOptions()
    {
        var holdout = new Foldout { text = "facialEditor.options.label".LS(), value = false };

        var addZeroWeightToggle = new Toggle("facialEditor.addZeroWeight.option".LS()) { value = AddZeroWeight };
        addZeroWeightToggle.RegisterValueChangedCallback(evt =>
        {
            AddZeroWeight = evt.newValue;
        });

        var addBaseSetToggle = new Toggle("facialEditor.addBaseSet.option".LS()) { value = AddBaseSet };
        addBaseSetToggle.RegisterValueChangedCallback(evt =>
        {
            AddBaseSet = evt.newValue;
        });

        var excludeTrackedShapesToggle = new Toggle("facialEditor.excludeTrackedShapes.option".LS()) { value = ExcludeTrackedShapes };
        excludeTrackedShapesToggle.RegisterValueChangedCallback(evt =>
        {
            ExcludeTrackedShapes = evt.newValue;
        });

        holdout.Add(addZeroWeightToggle);
        holdout.Add(addBaseSetToggle);
        holdout.Add(excludeTrackedShapesToggle);

        return holdout;
    }
}

internal abstract class ExpressionDataTargetingBase<T> : IShapesEditorTargeting<T> where T : Component, IHasExpressionData
{
    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new Exception("Target is not set");
        var result = new BlendShapeWeightSet();
        dataManager.GetCurrentOverrides(result);
        var animations = result.ToBlendShapeAnimations().ToList();
        var getProperty = (SerializedObject so) => so
            .FindProperty(nameof(IHasExpressionData.Data))
            .FindPropertyRelative(nameof(ExpressionData.BlendShapeAnimations));
        var serializedObject = new SerializedObject(Target);
        serializedObject.Update();
        var blendShapeAnimations = getProperty(serializedObject);
        ExpressionGUI.SetBlendShapeAnimations(blendShapeAnimations, animations);
        serializedObject.ApplyModifiedProperties();
    }
}

internal sealed class ExpressionDataTargeting : ExpressionDataTargetingBase<DataComponent>
{
    public override DataComponent? Target { get; set; }
}

internal sealed class FaceTuneDataTargeting : ExpressionDataTargetingBase<FaceTuneComponent>
{
    public override FaceTuneComponent? Target { get; set; }
}

internal sealed class FacialStyleTargeting : ExpressionDataTargetingBase<StyleComponent>
{
    public override StyleComponent? Target { get; set; }
}

internal class AdvancedLipSyncTargeting : IShapesEditorTargeting<LipSyncComponent>
{
    public override LipSyncComponent? Target { get; set; } = null;

    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new Exception("Target is not set");
        var result = new BlendShapeWeightSet();
        dataManager.GetCurrentOverrides(result);
        var getProperty = (SerializedObject so) => so.FindProperty(nameof(LipSyncComponent.AdvancedLipSyncSettings)).FindPropertyRelative(AdvancedLipSyncSettings.CancelerBlendShapeNamesPropName);
        SetStringArray(Target, getProperty, result.Keys);
    }

}

