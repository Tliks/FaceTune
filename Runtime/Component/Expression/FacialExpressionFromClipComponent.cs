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
            BlendShapeSet blendShapes = new();
#if UNITY_EDITOR
            if (Clip != null)
            {
                var newBlendShapes = GetBlendShapesFromClip(Clip);
                if (!IncludeZeroWeight) newBlendShapes = newBlendShapes.Where(x => x.Weight > 0).ToArray();

                var mapping = new BlendShapeSet(context.DefaultBlendShapes).BlendShapes.Select((x, i) => (x.Name, i)).ToDictionary(x => x.Name, x => x.i);
                foreach (var blendShape in newBlendShapes)
                {
                    if (mapping.TryGetValue(blendShape.Name, out var index))
                    {
                        blendShapes.Add(new BlendShape(blendShape.Name, blendShape.Weight));
                    }
                }
            }
#endif
            if (AddDefault)
            {
                var defaultShapes = new BlendShapeSet(context.DefaultBlendShapes);
                blendShapes = defaultShapes.Add(blendShapes);
            }
            return new FacialExpression(blendShapes, AllowEyeBlink, AllowLipSync, name);
        }

#if UNITY_EDITOR
        // AnimationUtilityからコピー
        private static IEnumerable<BlendShape> GetBlendShapesFromClip(AnimationClip clip, bool first = true)
        {
            var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith("blendShape.")) continue;

                var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null && curve.keys.Length > 0)
                {
                    var name = binding.propertyName.Replace("blendShape.", string.Empty);
                    var weight = first ? curve.keys[0].value : curve.keys[curve.keys.Length - 1].value;
                    yield return new BlendShape(name, weight);
                }
            }
        }
#endif
    }
}