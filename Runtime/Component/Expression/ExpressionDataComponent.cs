namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class ExpressionDataComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Expression Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual

        // BledShape
        public bool IsSingleFrame = true;
        public List<BlendShapeAnimation> BlendShapeAnimations = new();

        // Generic
        public List<GenericAnimation> Animations = new();


        // FromAnimationClip
        
        public AnimationClip? Clip = null;
        public ClipExcludeOption ClipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;


        internal List<GenericAnimation> GetAnimations(SessionContext sessionContext, IObserveContext observeContext)
        {
            throw new NotImplementedException();
        }

        internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, IObserveContext observeContext)
        {
            throw new NotImplementedException();
        }
    }
}