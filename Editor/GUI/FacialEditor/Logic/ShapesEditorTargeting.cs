using nadena.dev.ndmf.runtime;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace aoyon.facetune.gui.shapes_editor;

internal abstract class IShapesEditorTargeting
{
    public event Action? OnTargetChanged;
    protected void RaiseTargetChanged() => OnTargetChanged?.Invoke();
    public abstract void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager);
    public abstract float InitialAddWeight { get; }
    public abstract VisualElement DrawTargeting(VisualElement root);
}

internal abstract class IShapesEditorTargeting<T> : IShapesEditorTargeting where T : Object
{
    public abstract T? Target { get; set; }
    public void SetTarget(T? target)
    {
        if (Target == target) return;
        Target = target;
        RaiseTargetChanged();
    }
    public override VisualElement DrawTargeting(VisualElement root)
    {
        var objectField = new ObjectField();
        objectField.objectType = typeof(T);
        objectField.RegisterValueChangedCallback(evt =>
        {
            SetTarget(evt.newValue as T);
        });
        root.Add(objectField);
        var innerOptions = DrawInnerOptions(root);
        if (innerOptions != null)
        {
            root.Add(innerOptions);
        }
        return objectField;
    }
    protected virtual VisualElement? DrawInnerOptions(VisualElement root)
    {
        return null;
    }
}

internal class AnimationClipTargeting : IShapesEditorTargeting<AnimationClip>
{
    public override AnimationClip? Target { get; set; } = null;
    public bool AddZeroWeight { get; set; } = false;
    public bool AddFacialStyle { get; set; } = false;
    public bool ExcludeTrackedShapes { get; set; } = true;

    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        var animations = new AnimationIndex();
        var path = RuntimeUtil.RelativePath(root, renderer.gameObject);
        if (path == null) throw new Exception("TargetRenderer is not a child of root");
        if (AddZeroWeight)
        {
            var zeroShapes = dataManager.AllKeys.Select(key => new BlendShape(key, 0f));
            animations.AddRange(zeroShapes.ToGenericAnimations(path));
        }
        if (AddFacialStyle)
        {
            animations.AddRange(dataManager.StyleSet.ToGenericAnimations(path));
        }
        var overrides = new BlendShapeSet();
        dataManager.GetCurrentOverrides(overrides);
        animations.AddRange(overrides.ToGenericAnimations(path));
    }

    public override float InitialAddWeight => 100f;

    protected override VisualElement? DrawInnerOptions(VisualElement root)
    {
        var addZeroWeightToggle = new Toggle("Add Zero Weight");
        addZeroWeightToggle.RegisterValueChangedCallback(evt =>
        {
            AddZeroWeight = evt.newValue;
        });
        var addFacialStyleToggle = new Toggle("Add Facial Style");
        addFacialStyleToggle.RegisterValueChangedCallback(evt =>
        {
            AddFacialStyle = evt.newValue;
        });
        var excludeTrackedShapesToggle = new Toggle("Exclude Tracked Shapes");
        excludeTrackedShapesToggle.RegisterValueChangedCallback(evt =>
        {
            ExcludeTrackedShapes = evt.newValue;
        });
        var container = new VisualElement();
        container.Add(addZeroWeightToggle);
        container.Add(addFacialStyleToggle);
        container.Add(excludeTrackedShapesToggle);
        return container;
    }
}

[Serializable]
internal class FacialDataTargeting : IShapesEditorTargeting<FacialDataComponent>
{
    public override FacialDataComponent? Target { get; set; } = null;

    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new Exception("Target is not set");
        var result = new BlendShapeSet();
        dataManager.GetCurrentOverrides(result);
        var blendshapeAnimations = result.ToBlendShapeAnimations().ToList();
        CustomEditorUtility.AddBlendShapeAnimations(
            Target,
            so => so.FindProperty(nameof(FacialDataComponent.BlendShapeAnimations)),
            blendshapeAnimations
        );
    }

    public override float InitialAddWeight => 100f;
}

internal class FacialStyleTargeting : IShapesEditorTargeting<FacialStyleComponent>
{
    public override FacialStyleComponent? Target { get; set; } = null;

    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new Exception("Target is not set");
        var result = new BlendShapeSet();
        dataManager.GetCurrentOverrides(result);
        var blendshapeAnimations = result.ToBlendShapeAnimations().ToList();
        CustomEditorUtility.AddBlendShapeAnimations(
            Target,
            so => so.FindProperty(nameof(FacialStyleComponent.BlendShapeAnimations)),
            blendshapeAnimations
        );
    }

    public override float InitialAddWeight => 100f;
}

internal class AdvancedEyeBlinkTargeting : IShapesEditorTargeting<AdvancedEyeBlinkComponent>
{
    public override AdvancedEyeBlinkComponent? Target { get; set; } = null;

    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new Exception("Target is not set");
        var result = new BlendShapeSet();
        dataManager.GetCurrentOverrides(result);
        CustomEditorUtility.AddShapesAsNames(
            Target, 
            so => so.FindProperty(nameof(AdvancedEyeBlinkComponent.AdvancedEyeBlinkSettings)), 
            result.Names.ToList()
        );
    }

    public override float InitialAddWeight => 0f;
}

internal class AdvancedLipSyncTargeting : IShapesEditorTargeting<AdvancedLipSyncComponent>
{
    public override AdvancedLipSyncComponent? Target { get; set; } = null;

    public override void Save(GameObject root, SkinnedMeshRenderer renderer, BlendShapeOverrideManager dataManager)
    {
        if (Target == null) throw new Exception("Target is not set");
        var result = new BlendShapeSet();
        dataManager.GetCurrentOverrides(result);
        CustomEditorUtility.AddShapesAsNames(
            Target, 
            so => so.FindProperty(nameof(AdvancedLipSyncComponent.AdvancedLipSyncSettings)), 
            result.Names.ToList()
        );
    }

    public override float InitialAddWeight => 0f;
}

