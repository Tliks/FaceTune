namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionComponent : ExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT Facial Expression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public TrackingPermission AllowEyeBlink = TrackingPermission.Disallow;
        public TrackingPermission AllowLipSync = TrackingPermission.Allow;
        public bool EnableBlending = false;
        public FacialExpressionType ExpressionType = FacialExpressionType.Manual;

        // Manual
        [SerializeField]
        private List<BlendShape> _blendShapes = new(); //シリアライズしているので非readonly
        public IReadOnlyList<BlendShape> BlendShapes { get => _blendShapes.AsReadOnly(); }

        // From Clip
        public AnimationClip? Clip;
        public ClipExcludeOption ClipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;

        Expression IExpressionProvider.ToExpression(FacialExpression defaultExpression, IObserveContext observeContext)
        {
            var set = new BlendShapeSet();
            var defaultSet = defaultExpression.BlendShapeSet;

            var enableBlending = observeContext.Observe(this, c => c.EnableBlending, (a, b) => a == b);
            if (enableBlending)
            {
                set.Add(defaultSet);
            }

            BlendShapeSet? newSet = null;
#if UNITY_EDITOR
            newSet = GetBlendShapeSet(defaultSet, observeContext);
#endif 
            if (newSet != null)
            {
                set.Add(newSet);
            }

            return new FacialExpression(set, AllowEyeBlink, AllowLipSync, name);
        }

#if UNITY_EDITOR
        internal BlendShapeSet? GetBlendShapeSet(BlendShapeSet defaultSet, IObserveContext observeContext)
        {
            var expressionType = observeContext.Observe(this, c => c.ExpressionType, (a, b) => a == b);
            BlendShapeSet? blendShapeSet = null;
            switch (expressionType)
            {
                case FacialExpressionType.Manual:
                    blendShapeSet = observeContext.Observe(this, c => c.BlendShapes.ToSet(), (a, b) => a == b);
                    break;
                case FacialExpressionType.FromClip:
                    var clip = observeContext.Observe(this, c => c.Clip, (a, b) => a == b);
                    if (clip == null) return null;
                    var clipExcludeOption = observeContext.Observe(this, c => c.ClipExcludeOption, (a, b) => a == b);
                    blendShapeSet = BlendShapeUtility.GetBlendShapeSetFromClip(clip, clipExcludeOption, defaultSet);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(expressionType), expressionType, null);
            }
            if (blendShapeSet == null || blendShapeSet.BlendShapes.Count() == 0) return null;
            return blendShapeSet;
        }
#endif
    }
}