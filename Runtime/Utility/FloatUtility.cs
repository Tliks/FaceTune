internal static class FloatUtility
{
    // https://github.com/anatawa12/AvatarOptimizer/blob/587ec3449c21e82ec1e8c9c61de75bb0a75ecc9d/Internal/Utils/Utils.Float.cs#L8-L67
    public static float NextFloat(float x)
    {
        // NaN or Infinity : there is no next value
        if (float.IsNaN(x) || float.IsInfinity(x))
            throw new ArgumentOutOfRangeException(nameof(x), "x must be finite number");
        // zero: special case
        if (x == 0) return float.Epsilon;

        // rest is normal or subnormal number
        var asInt = BitConverter.SingleToInt32Bits(x);
        asInt += asInt < 0 ? -1 : 1;
        return BitConverter.Int32BitsToSingle(asInt);
    }

    public static float PreviousFloat(float x)
    {
        // NaN or Infinity : there is no previous value
        if (float.IsNaN(x) || float.IsInfinity(x))
            throw new ArgumentOutOfRangeException(nameof(x), "x must be finite number");
        // zero: special case
        if (x == 0) return -float.Epsilon;

        // rest is normal or subnormal number
        var asInt = BitConverter.SingleToInt32Bits(x);
        asInt -= asInt < 0 ? -1 : 1;
        return BitConverter.Int32BitsToSingle(asInt);
    }
}