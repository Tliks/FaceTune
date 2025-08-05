namespace Aoyon.FaceTune;

public abstract class AbstractDataComponent : FaceTuneTagComponent
{
    internal abstract List<GenericAnimation> GetAnimations(SessionContext sessionContext);
    internal abstract void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, IObserveContext? observeContext = null);
}