namespace com.aoyon.facetune.animator;

// AAP(float)のヘルパー
// VRCではfloatは[-1, 1]の範囲に制限され、かつ1/127で区部される
// これを良い感じに扱う
internal static class VRCAAPHelper
{
    public static float IndexToValue(int index)
    {
        if (index < 0 || index > 255)
        {
            throw new ArgumentException($"Index must be between 0 and 255. {index}");
        }
        return (index / 127f) - 1f;
    }

    public static int ValueToIndex(float value)
    {
        if (value < -1f || value > 1f)
        {
            throw new ArgumentException($"Value must be between -1.0 and 1.0. {value}");
        }
        int index = Mathf.RoundToInt((value + 1f) * 127f);
        return Mathf.Clamp(index, 0, 255);
    }
}