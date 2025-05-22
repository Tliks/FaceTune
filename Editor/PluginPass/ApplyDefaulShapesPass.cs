using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ApplyDefaulShapesPass : Pass<ApplyDefaulShapesPass>
{
    public override string QualifiedName => "com.aoyon.facetune.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(BuildContext context)
    {
        var mainComponents = context.AvatarRootObject.GetComponentsInChildren<FaceTuneComponent>(false);
        if (mainComponents.Length == 0) return;
        if (mainComponents.Length > 1) throw new Exception("FaceTuneComponent is not unique");
        var mainComponent = mainComponents[0];

        if (!mainComponent.TryGetSessionContext(out var sessionContext)) return;

        var faceRenderer = sessionContext.FaceRenderer;
        var defaultBlendShapes = sessionContext.DefaultBlendShapes;

        MeshHelper.ApplyBlendShapes(faceRenderer, sessionContext.FaceMesh, defaultBlendShapes);

        /*
        // デフォルトの表情を適用したら、デフォルトの表情コンポーネントは不要
        // 後続のパスではその場にあるブレンドシェイプを適用する
        mainComponent.DefaultExpressionComponent = null;
        */
    }
}
