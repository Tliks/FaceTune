namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionComponent : FacialExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT Facial Expression";
        internal const string MenuPath = FaceTuneTagComponent.FTName + "/" + ComponentName;

        [SerializeField]
        private List<BlendShape> _blendShapes = new();
        public List<BlendShape> BlendShapes
        {
            get => _blendShapes;
            set => _blendShapes = value;
        }
        
        Expression? IExpressionProvider.ToExpression(SessionContext context)
        {
            var blendShapes = new BlendShapeSet(_blendShapes);
            
            if (AddDefault)
            {
                var defaultShapes = new BlendShapeSet(context.DefaultBlendShapes.ToList());
                blendShapes = defaultShapes.Merge(blendShapes);
            }

            if (blendShapes.BlendShapes.Any())
            {
                return new FacialExpression(blendShapes, AllowEyeBlink, AllowLipSync, name);
            }
            else
            {
                return null;
            }
        }
    }
}