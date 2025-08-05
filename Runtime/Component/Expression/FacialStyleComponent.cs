namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class FacialStyleComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Facial Style";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        public bool ApplyToRenderer = false;

        internal void GetBlendShapeAnimations(ICollection<BlendShapeWeightAnimation> resultToAdd, IObserveContext? observeContext = null)
        {
            observeContext?.Observe(this);
            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation);
            }
        }
   
        internal void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IObserveContext? observeContext = null)
        {
            observeContext?.Observe(this);
            foreach (var animation in BlendShapeAnimations)
            {
                resultToAdd.Add(animation.ToFirstFrameBlendShape());
            }
        }
    }
}