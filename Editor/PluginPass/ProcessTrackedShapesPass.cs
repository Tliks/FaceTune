using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ProcessTrackedShapesPass : Pass<ProcessTrackedShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.process-tracked-shapes";
    public override string DisplayName => "Process Tracked Shapes";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.Extension<FTPassContext>()!;
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;

        List<Expression> allExpressions = new();
        allExpressions.AddRange(sessionContext.DEC.GetAllExpressions());
        if (passContext.PatternData != null && !passContext.PatternData.IsEmpty)
        {
            allExpressions.AddRange(passContext.PatternData.GetAllExpressions());
        }

        var trackedShapes = passContext.PlatformSupport.GetTrackedBlendShape().ToHashSet();

        if (sessionContext.Root.GetComponentsInChildren<AllowTrackedBlendShapesComponent>(true).Any())
        {
            var shapeNames = sessionContext.FaceRenderer.GetBlendShapes(sessionContext.FaceMesh).Select(b => b.Name);
            var shapesToClone = trackedShapes.Intersect(shapeNames);
            _ = shapesToClone;
            if (!shapesToClone.Any()) return;
            var mapping = ModifyFaceMesh(sessionContext.FaceRenderer, shapesToClone.ToHashSet());
            ModifyData(allExpressions, sessionContext.BodyPath, mapping);
        }
        else
        {
            RemoveAndWarning(allExpressions, trackedShapes);
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

    private void ModifyData(IEnumerable<Expression> expressions, string targetPath, Dictionary<string, string> mapping)
    {
        foreach (var expression in expressions)
        {
            expression.AnimationIndex.ReplaceBlendShapeNames(targetPath, mapping);
        }
    }

    private void RemoveAndWarning(IEnumerable<Expression> expressions, HashSet<string> trackedShapes)
    {
        var shapesToRemove = new List<BlendShape>();
        var shapesToWarning = new List<BlendShape>();

        foreach (var expression in expressions)
        {
            var shapes = expression.AnimationIndex.GetAllFirstFrameBlendShapeSet();
            foreach (var shape in shapes.BlendShapes)
            {
                if (trackedShapes.Contains(shape.Name))
                {
                    shapesToRemove.Add(shape);
                    if (shape.Weight != 0)
                    {
                        shapesToWarning.Add(shape);
                    }
                }
            }

            expression.AnimationIndex.RemoveBlendShapes(shapesToRemove.Select(s => s.Name));

            if (shapesToWarning.Any())
            {
                var joinedShapes = string.Join(", ", shapesToWarning.Select(s => s.Name));
                Debug.LogWarning($"Expression {expression.Name} contains tracked blend shapes [{joinedShapes}]. These will be removed and may cause unintended behavior.");
                Debug.LogWarning($"Please either add AllowTrackedBlendShapesComponent or exclude these blend shapes.");
            }

            shapesToRemove.Clear();
            shapesToWarning.Clear();
        }
    }
}