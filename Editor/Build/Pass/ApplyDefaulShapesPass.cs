using nadena.dev.ndmf;

namespace aoyon.facetune.build;

internal class ApplyDefaulShapesPass : Pass<ApplyDefaulShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;

        var sessionContext = buildPassContext.SessionContext;
        
        var facialStyleComponents = sessionContext.Root
            .GetComponentsInChildren<FacialStyleComponent>(true)
            .Where(x => x.ApplyToRenderer);

        var componentCount = facialStyleComponents.Count();
        if (componentCount == 0) return;
        FacialStyleComponent target;
        if (componentCount > 1)
        {
            Debug.LogWarning("ApplyDefaulShapesPass: Multiple FacialStyleComponent with ApplyToRenderer are found");
            target = facialStyleComponents.Last();
        }
        else
        {
            target = facialStyleComponents.First();
        }

        var set = new BlendShapeSet();
        set.AddRange(sessionContext.ZeroBlendShapes);
        target.GetBlendShapes(set);

        var faceRenderer = sessionContext.FaceRenderer;
        var faceMesh = sessionContext.FaceMesh;
        var blendShapeCount = faceMesh.blendShapeCount;
        for (int i = 0; i < blendShapeCount; i++)
        {
            var name = faceMesh.GetBlendShapeName(i);
            if (set.TryGetValue(name, out var blendShape))
            {
                faceRenderer.SetBlendShapeWeight(i, blendShape.Weight);
            }
        }
    }
}
