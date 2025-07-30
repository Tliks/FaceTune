namespace aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class FacialStyleComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Facial Style";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        public bool ApplyToRenderer = false;

        internal void GetBlendShapeAnimations(ICollection<BlendShapeAnimation> resultToAdd, IObserveContext? observeContext = null)
        {
            observeContext?.Observe(this);
            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation);
            }
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