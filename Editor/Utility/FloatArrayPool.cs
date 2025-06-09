namespace com.aoyon.facetune;

internal static class FloatArrayPool
{
    private static readonly Dictionary<int, Stack<float[]>> _pools = new();

    public static float[] Get(int length)
    {
        if (_pools.TryGetValue(length, out var stack) && stack.Count > 0)
        {
            return stack.Pop();
        }
        return new float[length];
    }

    public static void Return(float[] array)
    {
        _pools.GetOrAddNew(array.Length).Push(array);
    }
}