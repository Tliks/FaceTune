using nadena.dev.ndmf;
using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.pass;

internal class BuildPass : Pass<BuildPass>
{
    public override string QualifiedName => "com.aoyon.facetune.build";

    public override string DisplayName => "Build FaceTune";

    protected override void Execute(BuildContext context)
    {
        var mainComponents = context.AvatarRootObject.GetComponentsInChildren<FaceTuneComponent>(false);
        if (mainComponents.Length == 0) return;
        if (mainComponents.Length > 1) throw new Exception("FaceTuneComponent is not unique");
        var mainComponent = mainComponents[0];

        if (!mainComponent.TryGetSessionContext(out var sessionContext)) return;

        Profiler.BeginSample("CollectPresetData");
        var presets = CollectPresetData(sessionContext);
        if (presets == null) { Debug.LogWarning("No preset data found"); return; }
        var expressions = presets.SelectMany(p => p.GetAllExpressions());
        Profiler.EndSample();

        // Generate a mesh with cloned disallowed blend shapes and replace the data
        Profiler.BeginSample("CloneDisallowedBlendShapes");
        var shapesToClone = SearchShapesToClone(sessionContext, expressions);
        var mapping = ModifyFaceMesh(sessionContext.FaceRenderer, shapesToClone);
        ModifyPresetData(expressions, mapping);
        Profiler.EndSample();

        // optionやmenuitemは一旦後回し

        Profiler.BeginSample("InstallPresetData");
        InstallPresetData(context, sessionContext, presets);
        Profiler.EndSample();

        foreach (var component in mainComponent.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            Object.DestroyImmediate(component);
        }
    }

    private List<Preset>? CollectPresetData(SessionContext context)
    {
        var presetComponents = context.Root.GetComponentsInChildren<PresetComponent>(false);
        var presets = presetComponents
            .Select(c => c.GetPreset(context))
            .UnityOfType<Preset>()
            .ToList();
        if (presets.Count == 0) return null;
        return presets;
    }

    private HashSet<string> SearchShapesToClone(SessionContext context, IEnumerable<Expression> expressions)
    {
        var disAllowedShapes = new HashSet<string>();
        if (!context.Root.GetComponentsInChildren<CloneDisallowedBlendShapesComponent>(false).Any()) return disAllowedShapes;
        disAllowedShapes.UnionWith(FTPlatformSupport.GetDisallowedBlendShape(context));
        disAllowedShapes.IntersectWith(context.FaceRenderer.GetBlendShapes().Select(b => b.Name));
        disAllowedShapes.IntersectWith(expressions.SelectMany(e => e.BlendShapeNames));
        return disAllowedShapes;
    }

    private Dictionary<string, string> ModifyFaceMesh(SkinnedMeshRenderer renderer, HashSet<string> shapesToClone)
    {
        var oldMesh = renderer.sharedMesh;
        var newMesh = Object.Instantiate(oldMesh);
        ObjectRegistry.RegisterReplacedObject(oldMesh, newMesh);
        var mapping = MeshHelper.CloneShapes(newMesh, shapesToClone);
        renderer.sharedMesh = newMesh;
        return mapping;
    }

    private void ModifyPresetData(IEnumerable<Expression> expressions, Dictionary<string, string> mapping)
    {
        foreach (var expression in expressions)
        {
            foreach (var (oldName, newName) in mapping)
            {
                expression.ReplaceBlendShapeName(oldName, newName);
            }
        }
    }

    private void InstallPresetData(BuildContext context, SessionContext sessionContext, IEnumerable<Preset> presets)
    {
        FTPlatformSupport.InstallPresets(context, sessionContext, presets);
    }
}