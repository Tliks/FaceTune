namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionComponent : FacialExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT Facial Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        [SerializeField]
        private List<BlendShape> _blendShapes = new();
        public ReadOnlyCollection<BlendShape> BlendShapes { get => _blendShapes.AsReadOnly(); }
        // use FacialExpressionEditorUtility for setter
        
        Expression? IExpressionProvider.ToExpression(SessionContext context)
        {
            var blendShapeSet = new BlendShapeSet(BlendShapes);
            
            if (AddDefault)
            {
                var defaultShapes = new BlendShapeSet(context.DefaultBlendShapes);
                blendShapeSet = defaultShapes.Add(blendShapeSet);
            }

            if (blendShapeSet.BlendShapes.Any())
            {
                return new FacialExpression(blendShapeSet, AllowEyeBlink, AllowLipSync, name);
            }
            else
            {
                return null;
            }
        }
    }
}