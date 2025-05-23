namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class FacialExpressionFromClipComponent : FacialExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT Facial Expression From Clip";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public AnimationClip? Clip;
        public bool IncludeZeroWeight = false;

        Expression? IExpressionProvider.ToExpression(SessionContext context)
        {
            //if (Clip == null) return null;
            BlendShapeSet blendShapes = new();
#if UNITY_EDITOR
            if (Clip != null)
            {
                var newBlendShapes = new BlendShapeSet(GetBlendShapesFromClip(Clip));
                if (!IncludeZeroWeight) newBlendShapes.RemoveZeroWeight();
                blendShapes.Add(newBlendShapes);
            }
#endif
            if (!EnableBlending)
            {
                blendShapes.Add(context.DefaultBlendShapes, BlendShapeSetOptions.PreferFormer);
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