namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionComponent : FacialExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT Facial Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        [SerializeField]
        private List<BlendShape> _blendShapes = new(); //シリアライズしているので非readonly
        public IReadOnlyList<BlendShape> BlendShapes { get => _blendShapes.AsReadOnly(); }
        public bool EnableBlending = false;

        Expression? IExpressionProvider.ToExpression(SessionContext context, IOberveContext observeContext)
        {
            var blendShapeSet = observeContext.Observe(this, c => c.BlendShapes.ToSet(), (a, b) => a == b);
            if (blendShapeSet == null || blendShapeSet.BlendShapes.Count() == 0) return null;

            var enableBlending = observeContext.Observe(this, c => c.EnableBlending, (a, b) => a == b);

            if (!enableBlending)
            {
                var defaultShapes = context.DefaultExpression.BlendShapeSet;
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