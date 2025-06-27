namespace com.aoyon.facetune;

internal interface IAnimationProvider
{
    List<GenericAnimation> GetAnimations(SessionContext sessionContext);
}