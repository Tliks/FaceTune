using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ProcessTrackedShapesPass : Pass<ProcessTrackedShapesPass>
{
    public override string QualifiedName => "com.aoyon.facetune.process-tracked-shapes";
    public override string DisplayName => "Process Tracked Shapes";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.Extension<FTPassContext>()!;
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;
        var presetData = passContext.PatternData;
        if (presetData == null) throw new InvalidOperationException("PatternData is null");

        var trackedShapes = platform.PlatformSupport.GetTrackedBlendShape(sessionContext.Root.transform);

        if (sessionContext.Root.GetComponentsInChildren<AllowTrackedBlendShapesComponent>(true).Any())
        {
            var shapeNames = sessionContext.FaceRenderer.GetBlendShapes(sessionContext.FaceMesh).Select(b => b.Name);
            var shapesToClone = trackedShapes.Intersect(shapeNames);
            _ = shapesToClone;
            if (!shapesToClone.Any()) return;
            var mapping = ModifyFaceMesh(sessionContext.FaceRenderer, shapesToClone.ToHashSet());
            ModifyData(sessionContext.DEC.GetAllExpressions(), mapping);
            ModifyData(presetData.GetAllExpressions(), mapping);
        }
        else
        {
            RemoveAndWarning(sessionContext.DEC.GetAllExpressions(), trackedShapes);
            RemoveAndWarning(presetData.GetAllExpressions(), trackedShapes);
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

    private void ModifyData(IEnumerable<Expression> expressions, Dictionary<string, string> mapping)
    {
        foreach (var expression in expressions)
        {
            expression.ReplaceBlendShapeNames(mapping);
        }
    }

    private void RemoveAndWarning(IEnumerable<Expression> expressions, IEnumerable<string> trackedShapes)
    {
        foreach (var expression in expressions)
        {
            var names = new HashSet<string>(expression.BlendShapeNames);
            var intersection = names.Intersect(trackedShapes);
            if (intersection.Any())
            {
                var joinedShapes = string.Join(", ", intersection);
                Debug.LogWarning($"Expression {expression.Name} contains tracked blend shapes [{joinedShapes}]. These will be removed and may cause unintended behavior.");
                Debug.LogWarning($"Please either add CloneTrackedBlendShapesComponent or exclude these blend shapes.");
                expression.RemoveShapes(intersection);
            }
        }
    }
}