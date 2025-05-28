namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class DefaultFacialExpressionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Default Facial Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public TrackingPermission AllowEyeBlink = TrackingPermission.Disallow;
        public TrackingPermission AllowLipSync = TrackingPermission.Allow;
        public List<BlendShape> BlendShapes = new();

        internal FacialExpression? GetDefaultExpression(IOberveContext observeContext)
        {
            var set = observeContext.Observe(this, c => c.BlendShapes.ToSet(), (a, b) => a == b);
            if (set == null || set.BlendShapes.Count() == 0) return null;
            return new FacialExpression(set, AllowEyeBlink, AllowLipSync, name);
        }
    }
}
