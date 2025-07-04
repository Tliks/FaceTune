namespace aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class FacialStyleComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Facial Style";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public bool IsSingleFrame = true;
        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        public bool AsDefault = true;

        internal IEnumerable<GenericAnimation> GetAnimations(SessionContext sessionContext)
        {
            return BlendShapeAnimations.Select(bs => bs.ToGeneric(sessionContext.BodyPath));
        }
        
        internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, IObserveContext? observeContext = null)
        {
            observeContext?.Observe(this);
            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation.ToFirstFrameBlendShape());
            }
        }
    }
}