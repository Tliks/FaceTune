namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class DefaultFacialExpressionComponent : FacialExpressionComponentBase
    {
        internal const string ComponentName = "FT Default Facial Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public List<BlendShape> BlendShapes = new();

        internal FacialExpression? GetDefaultExpression()
        {
            if (BlendShapes.Count == 0) return null;
            var blendShapeSet = new BlendShapeSet(BlendShapes);
            return new FacialExpression(blendShapeSet, AllowEyeBlink, AllowLipSync, name);
        }
    }
}
