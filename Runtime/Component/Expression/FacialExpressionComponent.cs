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

        Expression? IExpressionProvider.ToExpression(FacialExpression defaultExpression, IOberveContext observeContext)
        {
            var defaultSet = defaultExpression.BlendShapeSet;

            BlendShapeSet? blendShapeSet = null;
#if UNITY_EDITOR
            blendShapeSet = GetBlendShapeSet(defaultSet, observeContext);
#endif
            if (blendShapeSet == null) return null;

            var enableBlending = observeContext.Observe(this, c => c.EnableBlending, (a, b) => a == b);
            if (!enableBlending)
            {
                blendShapeSet = defaultSet.Add(blendShapeSet);
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

#if UNITY_EDITOR
        internal BlendShapeSet? GetBlendShapeSet(BlendShapeSet defaultSet, IOberveContext observeContext)
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
                    blendShapeSet = GetBlendShapeSetFromClip(clip, clipExcludeOption, defaultSet);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(expressionType), expressionType, null);
            }
            if (blendShapeSet == null || blendShapeSet.BlendShapes.Count() == 0) return null;
            return blendShapeSet;
        }

        internal static BlendShapeSet GetBlendShapeSetFromClip(AnimationClip clip, ClipExcludeOption clipExcludeOption, BlendShapeSet defaultSet)
        {
            var blendShapes = new BlendShapeSet(GetBlendShapesFromClip(clip));
            switch (clipExcludeOption)
            {
                case ClipExcludeOption.None:
                    break;
                case ClipExcludeOption.ExcludeZeroWeight:
                    blendShapes.RemoveZeroWeight();
                    break;
                case ClipExcludeOption.ExcludeDefault:
                    blendShapes = blendShapes.ToDiff(defaultSet);
                    break;
            }
            return blendShapes;
        }

        // AnimationUtilityからコピー
        private static List<BlendShape> GetBlendShapesFromClip(AnimationClip clip, bool first = true)
        {
            var blendShapes = new List<BlendShape>();
            var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith("blendShape.")) continue;

                var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null && curve.keys.Length > 0)
                {
                    var name = binding.propertyName.Replace("blendShape.", string.Empty);
                    var weight = first ? curve.keys[0].value : curve.keys[curve.keys.Length - 1].value;
                    blendShapes.Add(new BlendShape(name, weight));
                }
            }
            return blendShapes;
        }
#endif
    }
}