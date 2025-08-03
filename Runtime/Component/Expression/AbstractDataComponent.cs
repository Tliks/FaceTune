namespace aoyon.facetune;

public abstract class AbstractDataComponent : FaceTuneTagComponent
{
    internal abstract List<GenericAnimation> GetAnimations(SessionContext sessionContext);
    internal abstract void GetBlendShapes(ICollection<BlendShape> resultToAdd, IReadOnlyList<BlendShapeAnimation> facialAnimations, IObserveContext? observeContext = null);
}