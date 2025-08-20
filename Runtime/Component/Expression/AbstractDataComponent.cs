namespace Aoyon.FaceTune;

public abstract class AbstractDataComponent : FaceTuneTagComponent
{
    internal abstract void GetAnimations(AnimationSet resultToAdd, SessionContext sessionContext);
    internal abstract void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, IObserveContext? observeContext = null);
}