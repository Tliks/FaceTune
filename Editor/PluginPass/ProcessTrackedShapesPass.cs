using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ProcessTrackedShapesPass : AbstractBuildPass<ProcessTrackedShapesPass>
{
    public override string QualifiedName => "com.aoyon.facetune.process-tracked-shapes";
    public override string DisplayName => "Process Tracked Shapes";

    protected override void Execute(BuildPassContext context)
    {
        var sessionContext = context.SessionContext;
        var trackedShapes = platform.PlatformSupport.GetTrackedBlendShape(sessionContext);

        // トラッキングにより巻き戻し得るので、トラッキングの仕組みに依存するものの基本デフォルト表情として定義する意味がない
        // また、同一の効果により競合するのでクローンの対象でもない
        sessionContext.DefaultExpression.RemoveShapes(trackedShapes);

        var shapesToClone = trackedShapes.Intersect(sessionContext.FaceRenderer.GetBlendShapes(sessionContext.FaceMesh).Select(b => b.Name));

        if (sessionContext.Root.GetComponentsInChildren<CloneTrackedBlendShapesComponent>(false).Any())
        {
            var mapping = ModifyFaceMesh(sessionContext.FaceRenderer, shapesToClone.ToHashSet());
            ModifyData(context.PresetData, mapping);
        }
        else
        {
            Warning(context.PresetData, trackedShapes);
        }
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

    private void ModifyData(PresetData presetData, Dictionary<string, string> mapping)
    {
        var expressions = presetData.GetAllExpressions().ToList();        
        foreach (var expression in expressions)
        {
            expression.ReplaceBlendShapeNames(mapping);
        }
    }

    private void Warning(PresetData presetData, IEnumerable<string> trackedShapes)
    {
        var expressions = presetData.GetAllExpressions().ToList();   
        foreach (var expression in expressions)
        {
            var names = new HashSet<string>(expression.BlendShapeNames);
            var intersection = names.Intersect(trackedShapes);
            if (intersection.Any())
            {
                var joinedShapes = string.Join(", ", intersection);
                Debug.LogWarning($"Expression {expression.Name} contains tracked blend shapes [{joinedShapes}]. These will not be processed and may cause unintended behavior.");
                Debug.LogWarning($"Please either add CloneTrackedBlendShapesComponent or exclude these blend shapes.");
            }
        }
    }
}