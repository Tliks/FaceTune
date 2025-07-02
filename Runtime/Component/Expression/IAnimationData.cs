namespace aoyon.facetune;

internal interface IAnimationData
{
    List<GenericAnimation> GetAnimations(SessionContext sessionContext);
}