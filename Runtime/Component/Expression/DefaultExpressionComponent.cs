namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class DefaultExpressionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Default Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public TrackingPermission AllowEyeBlink = TrackingPermission.Disallow;
        public TrackingPermission AllowLipSync = TrackingPermission.Allow;
        public List<BlendShape> BlendShapes = new();

        internal FacialExpression? GetDefaultExpression()
        {
            if (BlendShapes.Count == 0) return null;
            var blendShapeSet = new BlendShapeSet(BlendShapes);
            return new FacialExpression(blendShapeSet, AllowEyeBlink, AllowLipSync, name);
        }
    }
}
