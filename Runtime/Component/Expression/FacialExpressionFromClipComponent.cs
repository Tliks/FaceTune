namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionFromClipComponent : FacialExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT Facial Expression From Clip";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public AnimationClip? Clip;
        public bool IncludeZeroWeight = false;
        public bool EnableBlending = false;

        Expression? IExpressionProvider.ToExpression(FacialExpression defaultExpression, IOberveContext observeContext)
        {
            var clip = observeContext.Observe(this, c => c.Clip, (a, b) => a == b);
            if (clip == null) return null;
            var includeZeroWeight = observeContext.Observe(this, c => c.IncludeZeroWeight, (a, b) => a == b);
            var enableBlending = observeContext.Observe(this, c => c.EnableBlending, (a, b) => a == b);

            var blendShapes = new BlendShapeSet();
#if UNITY_EDITOR
            if (clip != null)
            {
                var newBlendShapes = new BlendShapeSet(GetBlendShapesFromClip(clip));
                if (!IncludeZeroWeight) newBlendShapes.RemoveZeroWeight();
                blendShapes.Add(newBlendShapes);
            }
#endif
            if (!enableBlending)
            {
                blendShapes.Add(defaultExpression.BlendShapeSet, BlendShapeSetOptions.PreferFormer);
            }
            if (!blendShapes.BlendShapes.Any()) return null;
            return new FacialExpression(blendShapes, AllowEyeBlink, AllowLipSync, name);
        }

#if UNITY_EDITOR
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