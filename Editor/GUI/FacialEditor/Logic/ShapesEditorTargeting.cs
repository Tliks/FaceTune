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
    public abstract float InitialAddWeight { get; }
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
        var animations = new AnimationIndex();
        var path = RuntimeUtil.RelativePath(root, renderer.gameObject);
        if (path == null) throw new Exception("TargetRenderer is not a child of root");
        if (AddZeroWeight)
        {
            var zeroShapes = dataManager.AllKeys.Select(key => new BlendShapeWeight(key, 0f));
            animations.AddRange(zeroShapes.ToGenericAnimations(path));
        }
        if (AddBaseSet)
        {
            animations.AddRange(dataManager.BaseSet.ToGenericAnimations(path));
        }
        var overrides = new BlendShapeSet();
        dataManager.GetCurrentOverrides(overrides);
        animations.AddRange(overrides.ToGenericAnimations(path));

        Target.RemoveAllCurveBindings();
        Target.AddGenericAnimations(animations);
        Target.SaveChanges();
    }

    public override float InitialAddWeight => 100f;

    public override VisualElement? DrawOptions()
    {
        var holdout = new Foldout { text = "Options", value = false };

        var addZeroWeightToggle = new Toggle("Add Zero Weight") { value = AddZeroWeight };
        addZeroWeightToggle.RegisterValueChangedCallback(evt =>
        {
            AddZeroWeight = evt.newValue;
        });

        var addBaseSetToggle = new Toggle("Add Base Set") { value = AddBaseSet };
        addBaseSetToggle.RegisterValueChangedCallback(evt =>
        {
            AddBaseSet = evt.newValue;
        });

        var excludeTrackedShapesToggle = new Toggle("Exclude Tracked Shapes") { value = ExcludeTrackedShapes };
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
            so => so.FindProperty(nameof(AdvancedEyeBlinkComponent.AdvancedEyeBlinkSettings)).FindPropertyRelative(AdvancedEyeBlinkSettings.CancelerBlendShapeNamesPropName), 
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
            so => so.FindProperty(nameof(AdvancedLipSyncComponent.AdvancedLipSyncSettings)).FindPropertyRelative(AdvancedLipSyncSettings.CancelerBlendShapeNamesPropName), 
            result.Names.ToList()
        );
    }

    public override float InitialAddWeight => 0f;
}

