using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal class ProcessTrackedShapesPass : Pass<ProcessTrackedShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.process-tracked-shapes";
    public override string DisplayName => "Process Tracked Shapes";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;

        var sessionContext = buildPassContext.SessionContext;

        var patternData = context.GetState<PatternData>();

        List<AvatarExpression> allExpressions = new();
        if (!patternData.IsEmpty)
        {
            allExpressions.AddRange(patternData.GetAllExpressions());
        }

        var trackedShapes = buildPassContext.PlatformSupport.GetTrackedBlendShape().ToHashSet();

        if (sessionContext.Root.GetComponentsInChildren<AllowTrackedBlendShapesComponent>(true).Any())
        {
            var setteledShapes = allExpressions.SelectMany(e => e.AnimationSet.GetBlendShapeNames(sessionContext.BodyPath)).ToHashSet();

            var shapeNames = sessionContext.FaceRenderer.GetBlendShapes(sessionContext.FaceMesh).Select(b => b.Name);
            var shapesToClone = trackedShapes.Intersect(shapeNames);
            _ = shapesToClone;
            if (!shapesToClone.Any()) return;
            var mapping = MeshHelper.CloneShapes(sessionContext.FaceRenderer, shapesToClone.ToHashSet(), (o, n) => ObjectRegistry.RegisterReplacedObject(o, n), _ => {}, "_clone.tracked");
            ModifyData(allExpressions, sessionContext.BodyPath, mapping);
        }
        else
        {
            RemoveAndWarning(allExpressions, trackedShapes);
        }
    }

    private void ModifyData(IEnumerable<AvatarExpression> expressions, string targetPath, Dictionary<string, string> mapping)
    {
        foreach (var expression in expressions)
        {
            expression.AnimationSet.ReplaceBlendShapeNames(targetPath, mapping);
        }
    }

    private void RemoveAndWarning(IEnumerable<AvatarExpression> expressions, HashSet<string> trackedShapes)
    {
        foreach (var expression in expressions)
        {
            var removed = expression.AnimationSet.RemoveBlendShapes(trackedShapes);

            if (removed.Any())
            {
                var joinedShapes = string.Join(", ", removed);
                Debug.LogWarning($"Expression {expression.Name} contains tracked blend shapes [{joinedShapes}]. These will be removed and may cause unintended behavior.");
                Debug.LogWarning($"Please either add AllowTrackedBlendShapesComponent or exclude these blend shapes.");
            }
        }
    }
}