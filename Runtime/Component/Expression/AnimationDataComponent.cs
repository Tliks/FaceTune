namespace aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class AnimationDataComponent : AbstractDataComponent
    {
        internal const string ComponentName = "FT Animation Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        /*
        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual
        public List<GenericAnimation> Animations = new();
        */

        // FromAnimationClip
        public AnimationClip? Clip = null;

        internal override List<GenericAnimation> GetAnimations(SessionContext sessionContext)
        {
            var animations = new List<GenericAnimation>();
            /*
            switch (SourceMode)
            {
                case AnimationSourceMode.Manual:
                    animations.AddRange(Animations);
                    break;
                case AnimationSourceMode.AnimationClip:
                    ClipToManual(animations);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(SourceMode), SourceMode, null);
            }
            */
            ClipToManual(animations);
            return animations;
        }

        internal void ClipToManual(List<GenericAnimation> animations)
        {
            if (Clip == null) return;
#if UNITY_EDITOR
            Clip.GetGenericAnimations(animations);
#endif
        }

        internal override void GetBlendShapes(ICollection<BlendShape> resultToAdd, IReadOnlyBlendShapeSet facialStyleSet, IObserveContext? observeContext = null)
        {
            return; // Todo
        }
    }
}