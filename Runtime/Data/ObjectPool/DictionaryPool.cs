namespace com.aoyon.facetune;

internal sealed class DictionaryPool<TKey, TValue> :
    CollectionPool<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>
{
}