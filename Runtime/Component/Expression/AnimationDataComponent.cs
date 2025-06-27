
namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class AnimationDataComponent : FaceTuneTagComponent, IAnimationProvider
    {
        internal const string ComponentName = "FT Animation Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public AnimationSourceMode SourceMode = AnimationSourceMode.Manual;

        // Manual
        public List<GenericAnimation> Animations = new();

        // FromAnimationClip
        public AnimationClip? Clip = null;

        List<GenericAnimation> IAnimationProvider.GetAnimations(SessionContext sessionContext)
        {
            var animations = new List<GenericAnimation>();
            switch (SourceMode)
            {
                case AnimationSourceMode.Manual:
                    animations.AddRange(Animations);
                    break;
                case AnimationSourceMode.FromAnimationClip:
                    if (Clip == null) break;
                    Clip.GetGenericAnimations(animations);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(SourceMode), SourceMode, null);
            }
            return animations;
        }
    }
}